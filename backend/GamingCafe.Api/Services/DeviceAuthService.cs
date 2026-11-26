using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using Microsoft.EntityFrameworkCore;
namespace GamingCafe.Api.Services;
public sealed class DeviceAuthService(GamingCafeDbContext db, JwtTokenService jwt)
{
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
