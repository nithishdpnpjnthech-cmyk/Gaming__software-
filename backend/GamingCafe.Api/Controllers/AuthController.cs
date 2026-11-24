using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
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

    // Dev-only instant admin token generator for frontend testing
    [HttpPost("dev-token")]
    public async Task<IActionResult> GenerateDevToken(
        [FromServices] IHostEnvironment env,
        [FromServices] JwtTokenService jwtTokenService,
        [FromServices] GamingCafeDbContext context)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        var devUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "dev-admin");
        if (devUser == null)
        {
            devUser = new User
            {
                Id = Guid.Parse("867ab196-49b0-46d5-a7f5-81444b7dc682"),
                Username = "dev-admin",
                Email = "admin@gamingcafe.local",
                PasswordHash = "DEV_HASH",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(devUser);

            var superAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Code == "SUPER_ADMIN");
            if (superAdminRole != null)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = devUser.Id,
                    RoleId = superAdminRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        var roles = new[] { "SUPER_ADMIN", "ADMIN" };
        var permissions = new[]
        {
            "dashboard.view",
            "devices.view",
            "devices.approve",
            "devices.reject",
            "devices.send_command"
        };

        var token = jwtTokenService.CreateToken(devUser, roles, permissions);
        return Ok(token);
    }
}