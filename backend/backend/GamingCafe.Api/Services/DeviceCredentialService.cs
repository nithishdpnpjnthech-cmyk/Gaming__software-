
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GamingCafe.Api.Services;

public sealed class DeviceCredentialService(
    GamingCafeDbContext db)
{
    // Normalize UTC timestamps to PostgreSQL microsecond precision.
    private static DateTime GetUtcNow()
    {
        var now = DateTime.UtcNow;

        return new DateTime(
            now.Ticks - (now.Ticks % 10),
            DateTimeKind.Utc);
    }

    private static DeviceCredentialResponse Map(
        DeviceCredential c) => new(
        c.Id,
        c.DeviceId,
        c.CertificateThumbprint,
        c.CertificateSerialNumber,
        c.CertificateSubject,
        c.CredentialStatus,
        c.IssuedAt,
        c.ExpiresAt,
        c.RevokedAt,
        c.CreatedAt
    );

    public async Task<DeviceCredentialResponse> EnrollAsync(
        Guid deviceId,
        Guid adminId,
        DeviceCredentialEnrollmentRequest request,
        CancellationToken ct)
    {
        var raw = Convert.FromBase64String(
            request.CertificateBase64);

        using var certificate =
            X509CertificateLoader.LoadCertificate(raw);

        // Use PostgreSQL-compatible UTC timestamp.
        var now = GetUtcNow();

        if (certificate.NotBefore.ToUniversalTime() > now ||
            certificate.NotAfter.ToUniversalTime() <= now)
        {
            throw new ArgumentException(
                "Certificate is expired or not yet valid.");
        }

        var thumbprint = certificate.GetCertHashString(
            HashAlgorithmName.SHA256);

        await using var tx =
            await db.Database.BeginTransactionAsync(ct);

        var devices = await db.Devices
            .FromSqlInterpolated(
                $"SELECT * FROM public.devices WHERE id = {deviceId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);

        var device = devices.SingleOrDefault();

        if (device is null)
            throw new KeyNotFoundException("Device not found.");

        if (device.RegistrationStatus != "APPROVED")
            throw new InvalidOperationException(
                "Device must be approved before enrollment.");

        var duplicate = await db.DeviceCredentials.AnyAsync(
            c => c.CertificateThumbprint == thumbprint, ct);

        if (duplicate)
            throw new InvalidOperationException(
                "Certificate is already enrolled.");

        var credential = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = thumbprint,
            CertificateSerialNumber = certificate.SerialNumber,
            CertificateSubject = certificate.Subject,
            CredentialStatus = "PENDING",
            IssuedAt = certificate.NotBefore.ToUniversalTime(),
            ExpiresAt = certificate.NotAfter.ToUniversalTime(),
            CreatedAt = now
        };

        db.DeviceCredentials.Add(credential);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = adminId,
            Action = "DEVICE_CREDENTIAL_ENROLLED",
            TargetType = "DEVICE",
            TargetId = deviceId,
            Details = JsonSerializer.Serialize(new
            {
                credentialId = credential.Id,
                thumbprint,
                status = "PENDING"
            }),
            OccurredAt = now
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName ==
                "device_credentials_certificate_thumbprint_key")
        {
            throw new InvalidOperationException(
                "Certificate is already enrolled.");
        }

        return Map(credential);
    }

    public async Task<List<DeviceCredentialResponse>> GetAllAsync(
        Guid deviceId,
        CancellationToken ct)
    {
        var exists = await db.Devices.AnyAsync(
            d => d.Id == deviceId, ct);

        if (!exists)
            throw new KeyNotFoundException("Device not found.");

        var credentials = await db.DeviceCredentials
            .AsNoTracking()
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return credentials.Select(Map).ToList();
    }

    public async Task<DeviceCredentialResponse?> GetAsync(
        Guid deviceId,
        Guid credentialId,
        CancellationToken ct)
    {
        var credential = await db.DeviceCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(c =>
                c.DeviceId == deviceId &&
                c.Id == credentialId, ct);

        return credential is null ? null : Map(credential);
    }

    public async Task<DeviceCredentialResponse> RevokeAsync(
        Guid deviceId,
        Guid credentialId,
        Guid adminId,
        CancellationToken ct)
    {
        await using var tx =
            await db.Database.BeginTransactionAsync(ct);

        var credentials = await db.DeviceCredentials
            .FromSqlInterpolated(
                $"SELECT * FROM public.device_credentials WHERE id = {credentialId} AND device_id = {deviceId} FOR UPDATE")
            .ToListAsync(ct);

        var credential = credentials.SingleOrDefault();

        if (credential is null)
            throw new KeyNotFoundException(
                "Credential not found.");

        // Idempotency: return the existing revoked credential.
        if (credential.CredentialStatus == "REVOKED")
            return Map(credential);

        // Use PostgreSQL-compatible UTC timestamp.
        var now = GetUtcNow();

        credential.CredentialStatus = "REVOKED";
        credential.RevokedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            UserId = adminId,
            Action = "DEVICE_CREDENTIAL_REVOKED",
            TargetType = "DEVICE",
            TargetId = deviceId,
            Details = JsonSerializer.Serialize(new
            {
                credentialId,
                status = "REVOKED"
            }),
            OccurredAt = now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Map(credential);
    }
}
