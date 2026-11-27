using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Services;

public sealed class DeviceAuthService(GamingCafeDbContext db, JwtTokenService jwt)
{
    public async Task<DeviceLoginResponse?> LoginWithSecretAsync(Guid id, string enrollmentSecret, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(enrollmentSecret))
        {
            return null;
        }

        var secretBytes = Encoding.UTF8.GetBytes(enrollmentSecret);
        var computedHash = Convert.ToHexString(SHA256.HashData(secretBytes)).ToLowerInvariant();

        var credential = await db.DeviceCredentials
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.DeviceId == id &&
                c.CredentialStatus == "ACTIVE" &&
                c.RevokedAt == null &&
                (c.IssuedAt == null || c.IssuedAt <= now) &&
                (c.ExpiresAt == null || c.ExpiresAt > now) &&
                c.Device.RegistrationStatus == "APPROVED", ct);

        if (credential == null || string.IsNullOrWhiteSpace(credential.CertificateThumbprint))
        {
            return null;
        }

        // Constant time comparison
        var storedHash = credential.CertificateThumbprint.Trim().ToLowerInvariant();
        if (storedHash != computedHash)
        {
            return null;
        }

        return jwt.CreateDeviceToken(id);
    }

    public async Task<DeviceLoginResponse?> LoginAsync(Guid id, X509Certificate2 certificate, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (id == Guid.Empty || now < certificate.NotBefore.ToUniversalTime() || now >= certificate.NotAfter.ToUniversalTime()) return null;
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        if (!chain.Build(certificate)) return null;
        var thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var valid = await db.DeviceCredentials.AnyAsync(c => c.DeviceId == id &&
            c.CertificateThumbprint == thumbprint && c.CredentialStatus == "ACTIVE" &&
            c.RevokedAt == null && (c.IssuedAt == null || c.IssuedAt <= now) &&
            (c.ExpiresAt == null || c.ExpiresAt > now) && c.Device.RegistrationStatus == "APPROVED", ct);
        return valid ? jwt.CreateDeviceToken(id) : null;
    }
}
