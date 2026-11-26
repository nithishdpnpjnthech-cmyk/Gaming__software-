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
}