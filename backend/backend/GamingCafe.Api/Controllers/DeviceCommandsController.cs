using System.Security.Claims;
using GamingCafe.Api.Data;
using GamingCafe.Api.Hubs;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Controllers;

public sealed record SendDeviceCommandRequest(
    string CommandType,
    string? Reason,
    int? TimeoutSeconds
);

[ApiController]
[Route("api/devices/{deviceId:guid}/commands")]
public sealed class DeviceCommandsController(
    GamingCafeDbContext db,
    IHubContext<AgentHub> hubContext,
    ILogger<DeviceCommandsController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "devices.send_command")]
    public async Task<IActionResult> DispatchCommand(
        Guid deviceId,
        [FromBody] SendDeviceCommandRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CommandType))
        {
            return BadRequest(new { message = "CommandType is required." });
        }

        var device = await db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct);

        if (device == null)
        {
            return NotFound(new { message = "Device not found." });
        }

        if (device.RegistrationStatus != "APPROVED")
        {
            return BadRequest(new { message = "Commands can only be dispatched to APPROVED devices." });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var timeout = Math.Clamp(request.TimeoutSeconds ?? 300, 10, 86400);

        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RequestedBy = userId,
            CommandType = request.CommandType.Trim().ToUpperInvariant(),
            CommandStatus = "PENDING",
            Reason = request.Reason,
            RequestedAt = now,
            ExpiresAt = now.AddSeconds(timeout)
        };

        db.DeviceCommands.Add(command);

        db.DeviceCommandLogs.Add(new DeviceCommandLog
        {
            CommandId = command.Id,
            EventType = "CREATED",
            Details = $"Command {command.CommandType} queued by admin {userId}.",
            EventTime = now
        });

        // If the device is currently connected to SignalR, dispatch immediately
        if (AgentHub.IsDeviceConnected(deviceId))
        {
            try
            {
                await hubContext.Clients.Group($"device:{deviceId}").SendAsync("ReceiveCommand", new
                {
                    CommandId = command.Id,
                    CommandType = command.CommandType,
                    Reason = command.Reason,
                    ExpiresAt = command.ExpiresAt
                }, ct);

                command.CommandStatus = "DISPATCHED";
                command.DispatchedAt = DateTime.UtcNow;

                db.DeviceCommandLogs.Add(new DeviceCommandLog
                {
                    CommandId = command.Id,
                    EventType = "DISPATCHED",
                    Details = "Command dispatched to active SignalR connection group.",
                    EventTime = DateTime.UtcNow
                });

                logger.LogInformation("Dispatched command {CommandId} ({CommandType}) to Device {DeviceId}.",
                    command.Id, command.CommandType, deviceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispatch command {CommandId} to Device {DeviceId} via SignalR.",
                    command.Id, deviceId);
            }
        }
        else
        {
            logger.LogInformation("Command {CommandId} ({CommandType}) queued for offline Device {DeviceId}.",
                command.Id, command.CommandType, deviceId);
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            command.Id,
            command.DeviceId,
            command.CommandType,
            command.CommandStatus,
            command.RequestedAt,
            command.ExpiresAt,
            command.DispatchedAt
        });
    }

    [HttpGet]
    [Authorize(Policy = "devices.view")]
    public async Task<IActionResult> GetCommands(Guid deviceId, CancellationToken ct)
    {
        var exists = await db.Devices.AnyAsync(d => d.Id == deviceId, ct);
        if (!exists)
        {
            return NotFound(new { message = "Device not found." });
        }

        var commands = await db.DeviceCommands
            .AsNoTracking()
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.RequestedAt)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.DeviceId,
                c.RequestedBy,
                c.CommandType,
                c.CommandStatus,
                c.Reason,
                c.RequestedAt,
                c.ExpiresAt,
                c.DispatchedAt,
                c.CompletedAt,
                c.ErrorMessage
            })
            .ToListAsync(ct);

        return Ok(commands);
    }
}
