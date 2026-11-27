
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Services;

public class AuthService
{
    private readonly GamingCafeDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        GamingCafeDbContext context,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // Reject unknown or inactive users.
        if (user == null || !user.IsActive)
        {
            return null;
        }

        // Verify the submitted password against the stored hash.
        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Collect the user's assigned roles.
        var roles = user.UserRoles
            .Select(ur => ur.Role.Code)
            .Distinct()
            .ToList();

        // Collect permissions from all assigned roles.
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToList();

        // Generate the JWT response.
        return _jwtTokenService.CreateToken(
            user,
            roles,
            permissions
        );
    }
}
