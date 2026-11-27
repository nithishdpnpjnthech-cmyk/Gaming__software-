using System.Security.Cryptography;
using System.Text;
using GamingCafe.Api.Data;
using GamingCafe.Api.DTOs;
using GamingCafe.Api.Hubs;
using GamingCafe.Api.Models;
using GamingCafe.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamingCafe.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DeviceConnectivityAndAuthIntegrationTests : IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestFixture _fixture;
    private readonly JwtTokenService _jwt;

    public DeviceConnectivityAndAuthIntegrationTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Issuer", "GamingCafe.Api"},
            {"Jwt:Audience", "GamingCafe.Clients"},
            {"Jwt:SecretKey", Convert.ToBase64String(Encoding.UTF8.GetBytes("SuperSecretLongKeyForGamingCafeBackend1234567890!"))},
            {"Jwt:ExpiryMinutes", "60"},
            {"DevDeviceAuth:Enabled", "true"}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwt = new JwtTokenService(config);
    }

    [Fact]
    public async Task DeviceRegistration_CreatesPendingDeviceAndInitialStatus()
    {
        await using var db = _fixture.CreateDbContext();
        var uniqueCode = $"REG-{Guid.NewGuid():N}"[..20];
        var fingerprint = $"FP-{Guid.NewGuid():N}";

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceCode = uniqueCode,
            DisplayName = "Test PC",
            Hostname = "TEST-PC-1",
            HardwareFingerprintHash = fingerprint,
            AgentVersion = "1.0.0",
            RegistrationStatus = "PENDING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Devices.Add(device);
        db.DeviceStatuses.Add(new DeviceStatus
        {
            DeviceId = device.Id,
            LastSeenAt = null,
            LastIpAddress = null,
            ReportedPowerState = null,
            LastStatusChangeAt = null,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var saved = await db.Devices.Include(d => d.DeviceStatus).FirstOrDefaultAsync(d => d.Id == device.Id);
        Assert.NotNull(saved);
        Assert.Equal("PENDING", saved.RegistrationStatus);
        Assert.NotNull(saved.DeviceStatus);
        Assert.Null(saved.DeviceStatus.LastSeenAt);
    }

    [Fact]
    public async Task DeviceApproval_UpdatesRegistrationStatusAndEnrollsCredential()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "PENDING");

        var device = await db.Devices.FirstAsync(d => d.Id == deviceId);
        var now = DateTime.UtcNow;

        device.RegistrationStatus = "APPROVED";
        device.ApprovedAt = now;
        device.UpdatedAt = now;

        var secret = $"approval-secret-{Guid.NewGuid():N}";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

        var credential = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = secretHash,
            CertificateSubject = "DevEnrollmentSecret",
            CredentialStatus = "ACTIVE",
            IssuedAt = now,
            ExpiresAt = now.AddYears(1),
            CreatedAt = now
        };
        db.DeviceCredentials.Add(credential);
        await db.SaveChangesAsync();

        var verified = await db.Devices.Include(d => d.DeviceCredentials).FirstAsync(d => d.Id == deviceId);
        Assert.Equal("APPROVED", verified.RegistrationStatus);
        Assert.Single(verified.DeviceCredentials);
        Assert.Equal("ACTIVE", verified.DeviceCredentials.First().CredentialStatus);
    }

    [Fact]
    public async Task DeviceAuthentication_ValidSecret_ReturnsJwtToken()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "APPROVED");

        var secret = $"valid-secret-{Guid.NewGuid():N}";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var now = DateTime.UtcNow;

        db.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = secretHash,
            CertificateSubject = "DevEnrollmentSecret",
            CredentialStatus = "ACTIVE",
            IssuedAt = now,
            ExpiresAt = now.AddYears(1),
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var authService = new DeviceAuthService(db, _jwt);
        var response = await authService.LoginWithSecretAsync(deviceId, secret, CancellationToken.None);

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(deviceId, response.DeviceId);
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task DeviceAuthentication_InvalidSecret_ReturnsNull()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "APPROVED");

        var secret = $"unique-secret-{Guid.NewGuid():N}";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var now = DateTime.UtcNow;

        db.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = secretHash,
            CertificateSubject = "DevEnrollmentSecret",
            CredentialStatus = "ACTIVE",
            IssuedAt = now,
            ExpiresAt = now.AddYears(1),
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var authService = new DeviceAuthService(db, _jwt);
        var response = await authService.LoginWithSecretAsync(deviceId, "wrong-secret", CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task DeviceAuthentication_RevokedCredential_ReturnsNull()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "APPROVED");

        var secret = $"revoked-secret-{Guid.NewGuid():N}";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var now = DateTime.UtcNow;

        db.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = secretHash,
            CertificateSubject = "DevEnrollmentSecret",
            CredentialStatus = "REVOKED",
            RevokedAt = now,
            IssuedAt = now.AddDays(-10),
            ExpiresAt = now.AddYears(1),
            CreatedAt = now.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var authService = new DeviceAuthService(db, _jwt);
        var response = await authService.LoginWithSecretAsync(deviceId, secret, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task DeviceAuthentication_PendingDevice_ReturnsNull()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "PENDING");

        var secret = $"pending-secret-{Guid.NewGuid():N}";
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var now = DateTime.UtcNow;

        db.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CertificateThumbprint = secretHash,
            CertificateSubject = "DevEnrollmentSecret",
            CredentialStatus = "ACTIVE",
            IssuedAt = now,
            ExpiresAt = now.AddYears(1),
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var authService = new DeviceAuthService(db, _jwt);
        var response = await authService.LoginWithSecretAsync(deviceId, secret, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task DeviceCommands_CanBeCreatedAndAuditedInDatabase()
    {
        await using var db = _fixture.CreateDbContext();
        var deviceId = await _fixture.CreateDeviceAsync(registrationStatus: "APPROVED");
        var now = DateTime.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync();
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"admin_{Guid.NewGuid():N}"[..20],
                Email = $"admin_{Guid.NewGuid():N}@test.com",
                PasswordHash = "hash",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RequestedBy = user.Id,
            CommandType = "RESTART",
            CommandStatus = "PENDING",
            Reason = "Admin test restart",
            RequestedAt = now,
            ExpiresAt = now.AddMinutes(5)
        };

        db.DeviceCommands.Add(command);
        db.DeviceCommandLogs.Add(new DeviceCommandLog
        {
            CommandId = command.Id,
            EventType = "CREATED",
            Details = "Command queued",
            EventTime = now
        });

        await db.SaveChangesAsync();

        var savedCmd = await db.DeviceCommands.FirstOrDefaultAsync(c => c.Id == command.Id);
        Assert.NotNull(savedCmd);
        Assert.Equal("RESTART", savedCmd.CommandType);
        Assert.Equal("PENDING", savedCmd.CommandStatus);

        var log = await db.DeviceCommandLogs.FirstOrDefaultAsync(l => l.CommandId == command.Id);
        Assert.NotNull(log);
        Assert.Equal("CREATED", log.EventType);
    }
}
