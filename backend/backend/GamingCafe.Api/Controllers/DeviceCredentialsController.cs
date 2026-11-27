using System.Security.Claims;
using System.Security.Cryptography;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/devices/{deviceId:guid}/credentials")]
public sealed class DeviceCredentialsController(
    DeviceCredentialService service) : ControllerBase
{
    [HttpPost]
    [Authorize(
        Roles = "SUPER_ADMIN",
        Policy = "devices.approve")]
    public async Task<IActionResult> Enroll(
        Guid deviceId,
        [FromBody] DeviceCredentialEnrollmentRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await service.EnrollAsync(
                deviceId, adminId, request, ct);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    deviceId,
                    credentialId = result.Id
                },
                result);
        }
        catch (FormatException)
        {
            return BadRequest(new
            {
                message = "Invalid Base64 certificate."
            });
        }
        catch (CryptographicException)
        {
            return BadRequest(new
            {
                message = "Invalid public certificate."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Policy = "devices.view")]
    public async Task<IActionResult> GetAll(
        Guid deviceId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetAllAsync(deviceId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{credentialId:guid}")]
    [Authorize(Policy = "devices.view")]
    public async Task<IActionResult> GetById(
        Guid deviceId,
        Guid credentialId,
        CancellationToken ct)
    {
        var result = await service.GetAsync(
            deviceId, credentialId, ct);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{credentialId:guid}/revoke")]
    [Authorize(
        Roles = "SUPER_ADMIN",
        Policy = "devices.approve")]
    public async Task<IActionResult> Revoke(
        Guid deviceId,
        Guid credentialId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await service.RevokeAsync(
                deviceId, credentialId, adminId, ct);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}