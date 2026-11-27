using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly GamingCafeDbContext _context;
    private readonly ILogger<DevicesController> _logger;
    private readonly IDeviceMonitoringConsole? _monitoringConsole;

    public DevicesController(
        GamingCafeDbContext context,
        ILogger<DevicesController> logger,
        IDeviceMonitoringConsole? monitoringConsole = null)
    {
        _context = context;
        _logger = logger;
        _monitoringConsole = monitoringConsole;
    }

    // Register a new gaming PC
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] DeviceRegistrationRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        _logger.LogInformation("[DISCOVERY] Agent registration attempt received. DisplayName: '{DisplayName}', DeviceCode: '{Code}', HardwareFingerprint: '{Fingerprint}'",
            request.DisplayName, request.DeviceCode, request.HardwareFingerprintHash);

        _monitoringConsole?.LogRegistrationRequest(
            request.DisplayName,
            request.DeviceCode,
            request.LocalDeviceId,
            clientIp,
            request.AgentVersion,
            request.HardwareFingerprintHash);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            _logger.LogWarning("Registration validation failed: DisplayName is empty.");
            _monitoringConsole?.LogRegistrationValidationResult(false, "Display name is required.");
            _monitoringConsole?.LogRegistrationFailure("Display name is required.", 400);
            return BadRequest("Display name is required.");
        }

        var displayName = request.DisplayName.Trim();
        if (displayName.Length > 100)
        {
            _logger.LogWarning("Registration validation failed: DisplayName exceeds max length.");
            _monitoringConsole?.LogRegistrationValidationResult(false, "Display name exceeds the allowed length.");
            _monitoringConsole?.LogRegistrationFailure("Display name exceeds the allowed length.", 400);
            return BadRequest("Display name exceeds the allowed length.");
        }

        // Idempotency check: if device with this HardwareFingerprintHash already exists, return it
        if (!string.IsNullOrWhiteSpace(request.HardwareFingerprintHash))
        {
            var existingByFingerprint = await _context.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.HardwareFingerprintHash == request.HardwareFingerprintHash);

            if (existingByFingerprint != null)
            {
                _logger.LogInformation("[REGISTRATION] Device already registered with fingerprint. Returning existing record. DeviceCode: '{Code}', BackendDeviceId: {Id}, Status: '{Status}'",
                    existingByFingerprint.DeviceCode, existingByFingerprint.Id, existingByFingerprint.RegistrationStatus);

                _monitoringConsole?.LogRegistrationValidationResult(true, "Device already registered with fingerprint.");
                _monitoringConsole?.LogRegistrationSuccess(
                    existingByFingerprint.Id,
                    existingByFingerprint.DeviceCode,
                    existingByFingerprint.DisplayName,
                    existingByFingerprint.RegistrationStatus,
                    $"/api/devices/{existingByFingerprint.Id}/approve",
                    isExistingRecord: true);

                return Ok(new
                {
                    existingByFingerprint.Id,
                    existingByFingerprint.DeviceCode,
                    existingByFingerprint.DisplayName,
                    existingByFingerprint.RegistrationStatus,
                    Message = "Device already registered. Returning existing registration record."
                });
            }
        }

        // If DeviceCode provided, validate length and check duplicates
        string deviceCode;
        if (!string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            deviceCode = request.DeviceCode.Trim().ToUpperInvariant();
            if (deviceCode.Length > 50)
            {
                _logger.LogWarning("Registration rejected: DeviceCode '{Code}' exceeds length limit.", deviceCode);
                _monitoringConsole?.LogRegistrationValidationResult(false, "Device code exceeds the allowed length.");
                _monitoringConsole?.LogRegistrationFailure("Device code exceeds the allowed length.", 400);
                return BadRequest("Device code exceeds the allowed length.");
            }

            if (await _context.Devices.AnyAsync(d => d.DeviceCode == deviceCode))
            {
                _logger.LogWarning("Registration conflict: DeviceCode '{Code}' already exists in database.", deviceCode);
                _monitoringConsole?.LogRegistrationValidationResult(false, $"DeviceCode '{deviceCode}' already exists in database.");
                _monitoringConsole?.LogRegistrationFailure("A device with this device code already exists.", 409);
                return Conflict("A device with this device code already exists.");
            }
        }
        else
        {
            // Auto-generate unique DeviceCode if client didn't supply one
            deviceCode = $"PC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            while (await _context.Devices.AnyAsync(d => d.DeviceCode == deviceCode))
            {
                deviceCode = $"PC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            }
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
                HardwareFingerprintHash = request.HardwareFingerprintHash,
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

            _logger.LogInformation("[REGISTRATION] New device registration received. DeviceCode: '{Code}', DisplayName: '{Name}'",
                device.DeviceCode, device.DisplayName);
            _logger.LogInformation("[REGISTRATION] Device record created. BackendDeviceId: {Id}, DeviceCode: '{Code}', Status: PENDING",
                device.Id, device.DeviceCode);
            _logger.LogInformation("[PENDING] Device awaiting administrator approval. BackendDeviceId: {Id}, DeviceCode: '{Code}'",
                device.Id, device.DeviceCode);

            _monitoringConsole?.LogRegistrationValidationResult(true, "Validation passed.");
            _monitoringConsole?.LogRegistrationSuccess(
                device.Id,
                device.DeviceCode,
                device.DisplayName,
                device.RegistrationStatus,
                $"/api/devices/{device.Id}/approve",
                isExistingRecord: false);

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

        _monitoringConsole?.LogApprovalRequest(id, userId, "POST /api/devices/{id}/approve");

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

            _logger.LogInformation("[APPROVAL] Administrator approved device. BackendDeviceId: {Id}, DeviceCode: '{Code}', ApprovedBy: {AdminId}",
                device.Id, device.DeviceCode, userId);
            _logger.LogInformation("[CREDENTIALS] Device credentials issued or activated. BackendDeviceId: {Id}, Status: ACTIVE",
                device.Id);

            // Generate cryptographically secure enrollment secret for this device
            var secretBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var enrollmentSecret = Convert.ToHexString(secretBytes).ToLowerInvariant();
            var secretHashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(enrollmentSecret));
            var secretHash = Convert.ToHexString(secretHashBytes).ToLowerInvariant();

            // Store or update credential with CertificateThumbprint
            var existingCredential = await _context.DeviceCredentials
                .FirstOrDefaultAsync(c => c.DeviceId == device.Id);

            if (existingCredential != null)
            {
                existingCredential.CertificateThumbprint = secretHash;
                existingCredential.CertificateSubject = "DevEnrollmentSecret";
                existingCredential.CredentialStatus = "ACTIVE";
                existingCredential.RevokedAt = null;
                existingCredential.IssuedAt = now;
                existingCredential.ExpiresAt = now.AddYears(1);
            }
            else
            {
                var newCredential = new DeviceCredential
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    CertificateThumbprint = secretHash,
                    CertificateSubject = "DevEnrollmentSecret",
                    CredentialStatus = "ACTIVE",
                    IssuedAt = now,
                    ExpiresAt = now.AddYears(1),
                    CreatedAt = now
                };
                _context.DeviceCredentials.Add(newCredential);
            }

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

            _monitoringConsole?.LogApprovalSuccess(
                device.Id,
                device.DeviceCode,
                userId,
                now,
                "ACTIVE",
                "POST /api/devices/{id}/approve",
                "POST /api/auth/device/dev-login");

            return Ok(new
            {
                Message = "Device approved successfully.",
                device.Id,
                device.DeviceCode,
                device.RegistrationStatus,
                device.ApprovedBy,
                device.ApprovedAt,
                EnrollmentSecret = enrollmentSecret
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

            _monitoringConsole?.LogRejection(
                device.Id,
                device.DeviceCode,
                userId,
                "Device registration rejected by administrator.");

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

    // Get an individual device (AllowAnonymous so Agent can verify its BackendDeviceId existence)
    [AllowAnonymous]
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

    // Agent check device registration / approval status (AllowAnonymous so Agent can poll its status)
    [AllowAnonymous]
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetDeviceStatus(Guid id)
    {
        var device = await _context.Devices
            .AsNoTracking()
            .Include(d => d.DeviceStatus)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound(new { message = "Device not found." });
        }

        var isOnline = device.DeviceStatus?.LastSeenAt.HasValue == true &&
                       (DateTime.UtcNow - device.DeviceStatus.LastSeenAt.Value).TotalSeconds <= 15;

        return Ok(new
        {
            device.Id,
            device.DeviceCode,
            device.DisplayName,
            device.RegistrationStatus,
            IsOnline = isOnline,
            device.DeviceStatus?.LastSeenAt,
            device.ApprovedAt
        });
    }

    // Securely claim enrollment secret once when approved
    [AllowAnonymous]
    [HttpPost("{id:guid}/claim-secret")]
    public async Task<IActionResult> ClaimEnrollmentSecret(Guid id, [FromBody] DeviceSecretClaimRequest request)
    {
        if (request == null || request.LocalDeviceId == Guid.Empty)
        {
            return BadRequest(new { message = "LocalDeviceId is required to claim enrollment secret." });
        }

        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound(new { message = "Device not found." });
        }

        if (device.RegistrationStatus != "APPROVED")
        {
            return Conflict(new { message = $"Device is not approved yet (Current status: {device.RegistrationStatus})." });
        }

        // Generate a new enrollment secret for this claiming agent
        var secretBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var enrollmentSecret = Convert.ToHexString(secretBytes).ToLowerInvariant();
        var secretHashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(enrollmentSecret));
        var secretHash = Convert.ToHexString(secretHashBytes).ToLowerInvariant();

        var now = DateTime.UtcNow;
        var credential = await _context.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == device.Id);

        if (credential != null)
        {
            credential.CertificateThumbprint = secretHash;
            credential.CertificateSubject = "DevEnrollmentSecret";
            credential.CredentialStatus = "ACTIVE";
            credential.RevokedAt = null;
            credential.IssuedAt = now;
            credential.ExpiresAt = now.AddYears(1);
        }
        else
        {
            credential = new DeviceCredential
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                CertificateThumbprint = secretHash,
                CertificateSubject = "DevEnrollmentSecret",
                CredentialStatus = "ACTIVE",
                IssuedAt = now,
                ExpiresAt = now.AddYears(1),
                CreatedAt = now
            };
            _context.DeviceCredentials.Add(credential);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("[CREDENTIALS] Device claimed enrollment credentials. BackendDeviceId: {Id}, DeviceCode: '{Code}'",
            device.Id, device.DeviceCode);

        return Ok(new
        {
            DeviceId = device.Id,
            EnrollmentSecret = enrollmentSecret,
            Message = "Enrollment secret provisioned successfully."
        });
    }
}
