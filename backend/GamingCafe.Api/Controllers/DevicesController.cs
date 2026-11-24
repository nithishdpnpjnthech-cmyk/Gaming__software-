using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Hubs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly GamingCafeDbContext _context;

    public DevicesController(GamingCafeDbContext context)
    {
        _context = context;
    }

    // Register a new gaming PC
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] DeviceRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(
                "Device code and display name are required.");
        }

        var deviceCode = request.DeviceCode.Trim();
        var displayName = request.DisplayName.Trim();

        if (deviceCode.Length > 50 || displayName.Length > 100)
        {
            return BadRequest(
                "Device code or display name exceeds the allowed length.");
        }

        if (await _context.Devices.AnyAsync(
            d => d.DeviceCode == deviceCode))
        {
            return Conflict(
                "A device with this device code already exists.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var device = new Device
            {
                DeviceCode = deviceCode,
                DisplayName = displayName,
                Hostname = request.Hostname,
                HardwareFingerprintHash =
                    request.HardwareFingerprintHash,
                AgentVersion = request.AgentVersion,
                RegistrationStatus = "PENDING"
            };

            _context.Devices.Add(device);

            var deviceStatus = new DeviceStatus
            {
                Device = device,
                LastSeenAt = null,
                LastIpAddress = null,
                ReportedPowerState = null,
                LastStatusChangeAt = null
            };

            _context.DeviceStatuses.Add(deviceStatus);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetDevice),
                new { id = device.Id },
                new
                {
                    device.Id,
                    device.DeviceCode,
                    device.DisplayName,
                    device.RegistrationStatus,
                    Message =
                        "Device registered successfully and is awaiting admin approval."
                });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Get all devices with live status
    [Authorize(Policy = "devices.view")]
    [HttpGet]
    public async Task<IActionResult> GetAllDevices()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Include(d => d.DeviceStatus)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.DeviceCode,
                d.DisplayName,
                d.Hostname,
                d.HardwareFingerprintHash,
                d.AgentVersion,
                d.RegistrationStatus,
                d.ApprovedAt,
                d.CreatedAt,
                Status = d.DeviceStatus != null ? new
                {
                    d.DeviceStatus.LastSeenAt,
                    LastIpAddress = d.DeviceStatus.LastIpAddress != null ? d.DeviceStatus.LastIpAddress.ToString() : null,
                    d.DeviceStatus.ReportedPowerState,
                    d.DeviceStatus.UpdatedAt
                } : null
            })
            .ToListAsync();

        return Ok(devices);
    }

    // Get devices waiting for approval
    [Authorize(Policy = "devices.view")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingDevices()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .Where(d => d.RegistrationStatus == "PENDING")
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return Ok(devices);
    }

    // Approve a pending device
    [Authorize(Policy = "devices.approve")]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveDevice(Guid id)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? userId = null;
        if (Guid.TryParse(userIdValue, out var parsedUserId))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == parsedUserId);
            if (userExists)
            {
                userId = parsedUserId;
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null)
            {
                return NotFound("Device not found.");
            }

            if (device.RegistrationStatus != "PENDING")
            {
                return Conflict(
                    "Only pending devices can be approved.");
            }

            device.RegistrationStatus = "APPROVED";
            device.ApprovedBy = userId;
            device.ApprovedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "DEVICE_APPROVED",
                TargetType = "DEVICE",
                TargetId = device.Id,
                Details = JsonSerializer.Serialize(new
{
    message = $"Device {device.DeviceCode} was approved.",
    deviceCode = device.DeviceCode,
    deviceId = device.Id
}),
                OccurredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                Message = "Device approved successfully.",
                device.Id,
                device.DeviceCode,
                device.RegistrationStatus,
                device.ApprovedBy,
                device.ApprovedAt
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Reject a pending device
    [Authorize(Policy = "devices.reject")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectDevice(Guid id)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? userId = null;
        if (Guid.TryParse(userIdValue, out var parsedUserId))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == parsedUserId);
            if (userExists)
            {
                userId = parsedUserId;
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null)
            {
                return NotFound("Device not found.");
            }

            if (device.RegistrationStatus != "PENDING")
            {
                return Conflict(
                    "Only pending devices can be rejected.");
            }

            device.RegistrationStatus = "REJECTED";
            device.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "DEVICE_REJECTED",
                TargetType = "DEVICE",
                TargetId = device.Id,
                Details = JsonSerializer.Serialize(new
{
    message = $"Device {device.DeviceCode} was rejected.",
    deviceCode = device.DeviceCode,
    deviceId = device.Id
}),
                OccurredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                Message = "Device rejected successfully.",
                device.Id,
                device.DeviceCode,
                device.RegistrationStatus
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Revoke an approved or active device
    [Authorize(Policy = "devices.reject")]
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(
        Guid id,
        [FromServices] IHubContext<AgentHub> hubContext)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? userId = null;
        if (Guid.TryParse(userIdValue, out var parsedUserId))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == parsedUserId);
            if (userExists)
            {
                userId = parsedUserId;
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var device = await _context.Devices
                .Include(d => d.DeviceStatus)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null)
            {
                return NotFound("Device not found.");
            }

            if (device.RegistrationStatus == "REVOKED")
            {
                return Conflict("Device is already revoked.");
            }

            device.RegistrationStatus = "REVOKED";
            device.UpdatedAt = DateTime.UtcNow;

            if (device.DeviceStatus != null)
            {
                device.DeviceStatus.ReportedPowerState = "SHUTDOWN";
                device.DeviceStatus.UpdatedAt = DateTime.UtcNow;
            }

            _context.DeviceConnectionLogs.Add(new DeviceConnectionLog
            {
                DeviceId = device.Id,
                EventType = "DISCONNECTED",
                EventTime = DateTime.UtcNow,
                Details = "Device enrollment was revoked by administrator."
            });

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "DEVICE_REVOKED",
                TargetType = "DEVICE",
                TargetId = device.Id,
                Details = JsonSerializer.Serialize(new
                {
                    message = $"Device {device.DeviceCode} was revoked.",
                    deviceCode = device.DeviceCode,
                    deviceId = device.Id
                }),
                OccurredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Broadcast real-time revocation event to the device SignalR group
            await hubContext.Clients.Group($"device_{device.Id:D}").SendAsync("DeviceRevocation", new
            {
                DeviceId = device.Id,
                Reason = "Device enrollment was revoked by administrator.",
                RevokedAtUtc = DateTimeOffset.UtcNow
            });

            return Ok(new
            {
                Message = "Device revoked successfully.",
                device.Id,
                device.DeviceCode,
                device.RegistrationStatus
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Re-authorize a revoked or rejected device (Option A: In-place recovery)
    [Authorize(Policy = "devices.approve")]
    [HttpPost("{id:guid}/reauthorize")]
    public async Task<IActionResult> ReauthorizeDevice(Guid id)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? userId = null;
        if (Guid.TryParse(userIdValue, out var parsedUserId))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == parsedUserId);
            if (userExists)
            {
                userId = parsedUserId;
            }
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null)
            {
                return NotFound("Device not found.");
            }

            if (device.RegistrationStatus == "APPROVED")
            {
                return Conflict("Device is already approved.");
            }

            device.RegistrationStatus = "APPROVED";
            device.ApprovedBy = userId;
            device.ApprovedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "DEVICE_REAUTHORIZED",
                TargetType = "DEVICE",
                TargetId = device.Id,
                Details = JsonSerializer.Serialize(new
                {
                    message = $"Device {device.DeviceCode} was re-authorized.",
                    deviceCode = device.DeviceCode,
                    deviceId = device.Id
                }),
                OccurredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                Message = "Device re-authorized successfully.",
                device.Id,
                device.DeviceCode,
                device.RegistrationStatus,
                device.ApprovedBy,
                device.ApprovedAt
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Authenticate an approved device
    [HttpPost("auth")]
    public async Task<IActionResult> AuthenticateDevice(
        [FromBody] DeviceAuthRequest request,
        [FromServices] JwtTokenService jwtTokenService)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode) ||
            string.IsNullOrWhiteSpace(request.HardwareFingerprintHash))
        {
            return BadRequest("Device code and hardware fingerprint are required.");
        }

        var deviceCode = request.DeviceCode.Trim();
        var fingerprint = request.HardwareFingerprintHash.Trim();

        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.DeviceCode == deviceCode);

        if (device == null)
        {
            return NotFound("Device not found.");
        }

        if (device.RegistrationStatus == "PENDING")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Device is awaiting administrator approval.",
                registrationStatus = device.RegistrationStatus
            });
        }

        if (device.RegistrationStatus != "APPROVED")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"Device registration status is '{device.RegistrationStatus}'. Connection not permitted.",
                registrationStatus = device.RegistrationStatus
            });
        }

        // Hardware fingerprint validation to prevent device cloning/spoofing
        if (!string.IsNullOrEmpty(device.HardwareFingerprintHash) &&
            !string.Equals(device.HardwareFingerprintHash, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            _context.SecurityEvents.Add(new SecurityEvent
            {
                DeviceId = device.Id,
                EventType = "AUTH_FINGERPRINT_MISMATCH",
                Outcome = "DENIED",
                Details = JsonSerializer.Serialize(new
                {
                    message = "Hardware fingerprint did not match registered device record.",
                    deviceCode = device.DeviceCode
                }),
                OccurredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                message = "Hardware fingerprint verification failed."
            });
        }

        var authResponse = jwtTokenService.CreateDeviceToken(device);

        _context.DeviceConnectionLogs.Add(new DeviceConnectionLog
        {
            DeviceId = device.Id,
            EventType = "AUTHENTICATED",
            EventTime = DateTime.UtcNow,
            Details = $"Device {device.DeviceCode} authenticated successfully via REST endpoint."
        });

        await _context.SaveChangesAsync();

        return Ok(authResponse);
    }

    // Get an individual device
    [Authorize(Policy = "devices.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDevice(Guid id)
    {
        var device = await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound();
        }

        return Ok(device);
    }
}
