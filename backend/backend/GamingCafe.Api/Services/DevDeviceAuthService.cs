using System.Security.Cryptography;
using System.Text;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Services;

public sealed class DevDeviceAuthService(
    GamingCafeDbContext db,
    JwtTokenService jwt,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    public bool IsEnabled =>
        environment.IsDevelopment() &&
        configuration.GetValue<bool>(
            "DevDeviceAuth:Enabled");

    public async Task<DeviceLoginResponse?> LoginAsync(
        DevDeviceLoginRequest request,
        CancellationToken ct)
    {
        if (!IsEnabled ||
            request.DeviceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                request.EnrollmentSecret))
        {
            return null;
        }

        var configuredHash = configuration[
            $"DevDeviceAuth:Devices:{request.DeviceId:D}:SecretHash"];

        if (string.IsNullOrWhiteSpace(configuredHash))
        {
            return null;
        }

        byte[] expectedHash;

        try
        {
            expectedHash =
                Convert.FromHexString(configuredHash);
        }
        catch (FormatException)
        {
            return null;
        }

        if (expectedHash.Length != 32)
        {
            return null;
        }

        var providedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                request.EnrollmentSecret));

        if (!CryptographicOperations.FixedTimeEquals(
                expectedHash,
                providedHash))
        {
            return null;
        }

        var approvedDevice = await db.Devices
            .AsNoTracking()
            .AnyAsync(
                d => d.Id == request.DeviceId &&
                     d.RegistrationStatus == "APPROVED",
                ct);

        if (!approvedDevice)
        {
            return null;
        }

        return jwt.CreateDeviceToken(
            request.DeviceId);
    }

    public async Task<DeviceLoginResponse?> LoginByDatabaseSecretAsync(
        DevDeviceLoginRequest request,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.EnrollmentSecret))
        {
            return null;
        }

        var secretBytes = Encoding.UTF8.GetBytes(request.EnrollmentSecret);
        var computedHash = Convert.ToHexString(SHA256.HashData(secretBytes)).ToLowerInvariant();
        var now = DateTime.UtcNow;

        var credential = await db.DeviceCredentials
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.DeviceId == request.DeviceId &&
                c.CredentialStatus == "ACTIVE" &&
                c.RevokedAt == null &&
                (c.IssuedAt == null || c.IssuedAt <= now) &&
                (c.ExpiresAt == null || c.ExpiresAt > now) &&
                c.Device.RegistrationStatus == "APPROVED", ct);

        if (credential == null || string.IsNullOrWhiteSpace(credential.CertificateThumbprint))
        {
            return null;
        }

        var storedHash = credential.CertificateThumbprint.Trim().ToLowerInvariant();
        if (storedHash != computedHash)
        {
            return null;
        }

        return jwt.CreateDeviceToken(request.DeviceId);
    }
}