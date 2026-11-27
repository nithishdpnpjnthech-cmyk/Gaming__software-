using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace GamingCafe.Api.Models;

public partial class DeviceNetworkInterface
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public string? InterfaceName { get; set; }

    public PhysicalAddress MacAddress { get; set; } = null!;

    public IPAddress? IpAddress { get; set; }

    public IPNetwork? SubnetCidr { get; set; }

    public bool IsPrimary { get; set; }

    public bool WolEnabled { get; set; }

    public bool? WolSupported { get; set; }

    public DateTime? LastReportedAt { get; set; }

    public virtual Device Device { get; set; } = null!;
}
