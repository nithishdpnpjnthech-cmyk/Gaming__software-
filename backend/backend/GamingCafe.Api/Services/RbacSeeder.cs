
using GamingCafe.Api.Data;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Services;

public static class RbacSeeder
{
    public static async Task SeedAsync(GamingCafeDbContext context)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        try
        {
            // 1. Define the initial permissions.
            var permissionDefinitions = new[]
            {
                ("dashboard.view", "dashboard", "view", "View dashboard"),

                ("devices.view", "devices", "view", "View devices"),
                ("devices.approve", "devices", "approve", "Approve devices"),
                ("devices.reject", "devices", "reject", "Reject devices"),
                ("devices.send_command", "devices", "send_command", "Send commands to devices"),

                ("users.view", "users", "view", "View users"),
                ("users.create", "users", "create", "Create users"),
                ("users.update", "users", "update", "Update users"),
                ("users.deactivate", "users", "deactivate", "Deactivate users"),

                ("roles.manage", "roles", "manage", "Manage roles"),
                ("permissions.manage", "permissions", "manage", "Manage permissions"),
                ("audit_logs.view", "audit_logs", "view", "View audit logs"),

                ("settings.view", "settings", "view", "View settings"),
                ("settings.update", "settings", "update", "Update settings")
            };

            // 2. Load existing permissions.
            var permissions = await context.Permissions.ToListAsync();

            foreach (var definition in permissionDefinitions)
            {
                var (code, module, action, description) = definition;

                var existing = permissions
                    .FirstOrDefault(p => p.Code == code);

                if (existing != null)
                {
                    if (existing.Module != module || existing.Action != action)
                    {
                        throw new InvalidOperationException(
                            $"Permission '{code}' has conflicting module/action values.");
                    }

                    continue;
                }

                // Prevent a duplicate module/action combination.
                if (permissions.Any(p =>
                    p.Module == module && p.Action == action))
                {
                    throw new InvalidOperationException(
                        $"Permission module/action already exists: {module}.{action}");
                }

                var permission = new Permission
                {
                    Code = code,
                    Module = module,
                    Action = action,
                    Description = description
                };

                context.Permissions.Add(permission);
                permissions.Add(permission);
            }

            // 3. Load the existing roles and their mappings.
            var roles = await context.Roles
                .Include(r => r.Permissions)
                .ToListAsync();

            var superAdmin = roles.FirstOrDefault(r => r.Code == "SUPER_ADMIN");
            var admin = roles.FirstOrDefault(r => r.Code == "ADMIN");
            var staff = roles.FirstOrDefault(r => r.Code == "STAFF");

            if (superAdmin == null || admin == null || staff == null)
            {
                throw new InvalidOperationException(
                    "One or more required system roles are missing.");
            }

            // 4. SUPER_ADMIN receives every defined permission.
            foreach (var permission in permissions)
            {
                if (!superAdmin.Permissions.Any(p => p.Code == permission.Code))
                {
                    superAdmin.Permissions.Add(permission);
                }
            }

            // 5. Define ADMIN permissions.
            var adminPermissionCodes = new[]
            {
                "dashboard.view",
                "devices.view",
                "devices.approve",
                "devices.reject",
                "devices.send_command",
                "users.view",
                "settings.view"
            };

            foreach (var code in adminPermissionCodes)
            {
                var permission = permissions.First(p => p.Code == code);

                if (!admin.Permissions.Any(p => p.Code == code))
                {
                    admin.Permissions.Add(permission);
                }
            }

            // 6. Define STAFF permissions.
            var staffPermissionCodes = new[]
            {
                "dashboard.view",
                "devices.view"
            };

            foreach (var code in staffPermissionCodes)
            {
                var permission = permissions.First(p => p.Code == code);

                if (!staff.Permissions.Any(p => p.Code == code))
                {
                    staff.Permissions.Add(permission);
                }
            }

            // 7. Save everything together.
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
