
using GamingCafe.Api.Data;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Services;

public static class SuperAdminBootstrapper
{
    public static async Task RunAsync(
        GamingCafeDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        Console.WriteLine("=== Super Admin Initial Setup ===");

        // Allow this setup only when no users exist.
        if (await context.Users.AnyAsync())
        {
            Console.WriteLine("Bootstrap stopped: a user already exists.");
            return;
        }

        var superAdminRole = await context.Roles
            .FirstOrDefaultAsync(r => r.Code == "SUPER_ADMIN");

        if (superAdminRole == null)
        {
            throw new InvalidOperationException(
                "SUPER_ADMIN role was not found. Run the RBAC seeder first.");
        }

        Console.Write("Enter Super Admin username: ");
        var username = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("Username cannot be empty.");
            return;
        }

        Console.Write("Enter email (optional): ");
        var email = Console.ReadLine()?.Trim();

        Console.Write("Enter password (minimum 12 characters): ");
        var password = ReadHiddenPassword();

        Console.WriteLine();
        Console.Write("Confirm password: ");
        var confirmation = ReadHiddenPassword();

        Console.WriteLine();

        if (password.Length < 12 || password != confirmation)
        {
            Console.WriteLine("Password is too short or passwords do not match.");
            return;
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash =
                passwordHasher.HashPassword(user, password);

            context.Users.Add(user);

            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = superAdminRole.Id,
                AssignedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine("Super Admin account created successfully.");
            Console.WriteLine($"Username: {user.Username}");
            Console.WriteLine("Role: SUPER_ADMIN");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static string ReadHiddenPassword()
    {
        var password = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                    password.Length--;

                continue;
            }

            if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }

        return password.ToString();
    }
}
