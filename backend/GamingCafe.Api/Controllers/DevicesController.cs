using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;

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

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
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

            if (device.RegistrationStatus != "PENDING")
            {
                return Conflict(
                    "Only pending devices can be approved.");
            }

            var now = DateTime.UtcNow;

            device.RegistrationStatus = "APPROVED";
            device.ApprovedBy = userId;
            device.ApprovedAt = now;
            device.UpdatedAt = now;

            if (device.DeviceStatus != null)
            {
                device.DeviceStatus.LastStatusChangeAt = now;
                device.DeviceStatus.UpdatedAt = now;
            }
            else
            {
                var deviceStatus = new DeviceStatus
                {
                    DeviceId = device.Id,
                    LastSeenAt = null,
                    LastIpAddress = null,
                    ReportedPowerState = null,
                    LastStatusChangeAt = now,
                    UpdatedAt = now
                };
                _context.DeviceStatuses.Add(deviceStatus);
            }

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
                OccurredAt = now
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

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
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
