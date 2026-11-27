using System.Net;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/auth/device")]
public sealed class DevDeviceAuthController(
    DevDeviceAuthService service,
    ILogger<DevDeviceAuthController> logger,
    IDeviceMonitoringConsole? monitoringConsole = null)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("dev-login")]
    public async Task<IActionResult> Login(
        [FromBody] DevDeviceLoginRequest request,
        CancellationToken ct)
    {
        var remoteIp =
            HttpContext.Connection.RemoteIpAddress;

        if (remoteIp?.IsIPv4MappedToIPv6 == true)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        logger.LogInformation("[AUTH] Device authentication requested for BackendDeviceId: {DeviceId}", request.DeviceId);
        monitoringConsole?.LogAuthenticationAttempt(request.DeviceId, remoteIp?.ToString());

        if (!service.IsEnabled)
        {
            logger.LogWarning("Device authentication rejected: DevDeviceAuth service is disabled in configuration.");
            monitoringConsole?.LogAuthenticationResult(request.DeviceId, false, "DevDeviceAuth service is disabled in configuration.");
            return NotFound();
        }

        var isLoopback =
            remoteIp is not null &&
            IPAddress.IsLoopback(remoteIp);

        // For isolated Development testing over LAN HTTP, allow HTTP login when DevDeviceAuth is enabled
        // In Production, HTTPS is strictly enforced.
        if (!Request.IsHttps && !isLoopback && !service.IsEnabled)
        {
            logger.LogWarning("Device authentication rejected: Remote device {DeviceId} attempted login over insecure HTTP.", request.DeviceId);
            monitoringConsole?.LogAuthenticationResult(request.DeviceId, false, "HTTPS is required for remote device login.");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "HTTPS is required for remote device login."
                });
        }

        if (request.DeviceId == Guid.Empty)
        {
            logger.LogWarning("Device authentication rejected: DeviceId is empty Guid.");
            monitoringConsole?.LogAuthenticationResult(request.DeviceId, false, "Valid device ID is required.");
            return BadRequest(new
            {
                message = "Valid device ID is required."
            });
        }

        var result = await service.LoginAsync(
            request,
            ct);

        // If config-based lookup did not find or match secret, check database credential secret_hash
        if (result is null)
        {
            result = await service.LoginByDatabaseSecretAsync(request, ct);
        }

        if (result is null)
        {
            logger.LogWarning("[AUTH] Device authentication FAILED for BackendDeviceId: {DeviceId}. Outcome: FAILED", request.DeviceId);
            monitoringConsole?.LogAuthenticationResult(request.DeviceId, false, "Invalid device credentials.");
            return Unauthorized(new
            {
                message = "Invalid device credentials."
            });
        }

        logger.LogInformation("[AUTH] Device authentication SUCCESS! JWT token issued for BackendDeviceId: {DeviceId} (ExpiresAt: {ExpiresUtc}), Outcome: SUCCESS",
            request.DeviceId, result.ExpiresAt);
        monitoringConsole?.LogAuthenticationResult(request.DeviceId, true, expiresAt: result.ExpiresAt);

        Response.Headers.CacheControl = "no-store";

        return Ok(result);
    }
}