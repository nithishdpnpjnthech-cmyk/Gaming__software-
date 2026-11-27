using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Models;

public partial class DeviceCredential
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public string? CertificateThumbprint { get; set; }

    public string? CertificateSerialNumber { get; set; }

    public string? CertificateSubject { get; set; }

    public string CredentialStatus { get; set; } = null!;

    public DateTime? IssuedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Device Device { get; set; } = null!;
}
