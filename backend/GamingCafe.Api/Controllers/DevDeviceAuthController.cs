using System.Net;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/auth/device")]
public sealed class DevDeviceAuthController(
    DevDeviceAuthService service)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("dev-login")]
    public async Task<IActionResult> Login(
        [FromBody] DevDeviceLoginRequest request,
        CancellationToken ct)
    {
        if (!service.IsEnabled)
        {
            return NotFound();
        }

        var remoteIp =
            HttpContext.Connection.RemoteIpAddress;

        if (remoteIp?.IsIPv4MappedToIPv6 == true)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        var isLoopback =
            remoteIp is not null &&
            IPAddress.IsLoopback(remoteIp);

        // HTTP is permitted only for local testing.
        // Remote Agent PCs must use HTTPS.
        if (!Request.IsHttps && !isLoopback)
        {
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
            return BadRequest(new
            {
                message = "Valid device ID is required."
            });
        }

        var result = await service.LoginAsync(
            request,
            ct);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid device credentials."
            });
        }

        Response.Headers.CacheControl = "no-store";

        return Ok(result);
    }
}