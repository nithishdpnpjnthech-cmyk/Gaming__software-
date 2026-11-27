using GamingCafe.Api.DTOs;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly DeviceAuthService _deviceAuth;

    public AuthController(AuthService authService, DeviceAuthService deviceAuth)
    {
        _authService = authService;
        _deviceAuth = deviceAuth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        return Ok(result);
    }
    [HttpPost("device/login")]
    public async Task<IActionResult> DeviceLogin([FromBody] DeviceLoginRequest request, CancellationToken ct)
    {
        if (!Request.IsHttps || request.DeviceId == Guid.Empty) return BadRequest("HTTPS and device ID required.");
        var certificate = await HttpContext.Connection.GetClientCertificateAsync(ct);
        if (certificate is null) return Unauthorized();
        var result = await _deviceAuth.LoginAsync(request.DeviceId, certificate, ct);
        return result is null ? Unauthorized() : Ok(result);
    }
}
