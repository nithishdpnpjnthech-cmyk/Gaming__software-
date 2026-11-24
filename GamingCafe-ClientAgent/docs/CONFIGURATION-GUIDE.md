# Gaming Cafe Client Agent — Configuration Guide

## 1. Configuration File Hierarchy

Settings are loaded in hierarchical order using `Microsoft.Extensions.Configuration`:
1. `appsettings.json` (Production defaults)
2. `appsettings.{Environment}.json` (e.g. `appsettings.Development.json`)
3. Environment variables (prefixed with `GAMINGCAFE_AGENT__`)
4. Command line arguments

---

## 2. Configuration Schema (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.AspNetCore.SignalR": "Warning"
    },
    "File": {
      "LogDirectory": "logs",
      "MaxFileSizeMb": 10,
      "RetainedFileDays": 14
    }
  },
  "Backend": {
    "HubUrl": "wss://admin.gamingcafe.local:5001/hubs/agent",
    "RequireWss": true,
    "ValidateCertificates": true,
    "ExpectedCertificateThumbprint": "",
    "ConnectionTimeoutSeconds": 30,
    "HandshakeTimeoutSeconds": 15
  },
  "Reconnection": {
    "InitialDelaySeconds": 2.0,
    "MaxDelaySeconds": 60.0,
    "BackoffMultiplier": 2.0,
    "JitterMaxSeconds": 3.0,
    "MaxReconnectAttempts": 0
  },
  "Heartbeat": {
    "IntervalSeconds": 15,
    "TimeoutSeconds": 10,
    "MaxMissedHeartbeats": 3
  },
  "Identity": {
    "StorageDirectory": "data",
    "ClientVersion": "1.0.0"
  },
  "Security": {
    "UseMachineScope": true,
    "RejectNonWss": true
  }
}
```

---

## 3. Configuration Reference Table

| Section | Setting | Default | Description |
|---|---|---|---|
| `Backend` | `HubUrl` | `wss://...` | Absolute URL to the backend SignalR Hub. Must use `wss://` or `https://`. |
| `Backend` | `RequireWss` | `true` | When true, rejects insecure `ws://` and `http://` schemes. |
| `Backend` | `ValidateCertificates` | `true` | Enforces TLS certificate validation via system trust store. |
| `Backend` | `ExpectedCertificateThumbprint` | `""` | Optional SHA-256 thumbprint for pinned certificate verification. |
| `Reconnection` | `InitialDelaySeconds` | `2.0` | Initial delay before the first reconnection retry. |
| `Reconnection` | `MaxDelaySeconds` | `60.0` | Maximum cap on exponential backoff delays. |
| `Reconnection` | `BackoffMultiplier` | `2.0` | Multiplier for exponential backoff ($2.0 = \text{doubling}$). |
| `Reconnection` | `JitterMaxSeconds` | `3.0` | Maximum random cryptographic jitter added to delay. |
| `Heartbeat` | `IntervalSeconds` | `15` | Frequency of heartbeat transmissions to the Hub. |
| `Heartbeat` | `MaxMissedHeartbeats` | `3` | Threshold of consecutive missed acks before marking connection stale. |
| `Identity` | `StorageDirectory` | `"data"` | Folder where `device-identity.json` and credentials are saved. |
| `Security` | `UseMachineScope` | `true` | Uses DPAPI `DataProtectionScope.LocalMachine` for Windows Services. |
