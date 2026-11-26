using System.ComponentModel.DataAnnotations;

namespace GamingCafe.Api.DTOs;

public sealed class DeviceCredentialEnrollmentRequest
{
    [Required]
    [StringLength(32768)]
    public string CertificateBase64 { get; set; } = string.Empty;
}

public sealed record DeviceCredentialResponse(
    Guid Id,
    Guid DeviceId,
    string CertificateThumbprint,
    string? CertificateSerialNumber,
    string? CertificateSubject,
    string CredentialStatus,
    DateTime? IssuedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt,
    DateTime CreatedAt
);