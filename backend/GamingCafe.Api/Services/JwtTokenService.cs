
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using GamingCafe.Api.Models;
using GamingCafe.Api.DTOs;

namespace GamingCafe.Api.Services;

public class JwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly byte[] _secretKey;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is missing.");

        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is missing.");

        var secret = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT secret key is missing.");

        try
        {
            _secretKey = Convert.FromBase64String(secret);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "JWT secret key must be a valid Base64 value.");
        }

        if (_secretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret key must contain at least 32 bytes.");
        }

        if (!int.TryParse(
            configuration["Jwt:ExpiryMinutes"],
            out _expiryMinutes) || _expiryMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiry minutes must be a positive number.");
        }
    }

    public LoginResponse CreateToken(
        User user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var roleList = roles.Distinct().ToList();
        var permissionList = permissions.Distinct().ToList();

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_expiryMinutes);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };

        foreach (var role in roleList)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissionList)
        {
            claims.Add(new Claim("permission", permission));
        }

        var signingKey = new SymmetricSecurityKey(_secretKey);

        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: signingCredentials
        );

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return new LoginResponse
        {
            AccessToken = tokenString,
            ExpiresAt = expiresAt,
            Username = user.Username,
            Roles = roleList,
            Permissions = permissionList
        };
    }

    public DeviceAuthResponse CreateDeviceToken(Device device)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(7);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(ClaimTypes.Name, device.DeviceCode),
            new Claim(ClaimTypes.Role, "Device"),
            new Claim("device_id", device.Id.ToString()),
            new Claim("device_code", device.DeviceCode),
            new Claim("permission", "devices.connect")
        };

        var signingKey = new SymmetricSecurityKey(_secretKey);

        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: signingCredentials
        );

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return new DeviceAuthResponse
        {
            AccessToken = tokenString,
            ExpiresAt = expiresAt,
            DeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            DisplayName = device.DisplayName
        };
    }
}
