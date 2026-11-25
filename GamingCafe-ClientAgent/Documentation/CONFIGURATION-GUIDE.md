# Centralized Configuration Guide: `appsettings.json`

**Module:** `GamingCafe.ClientAgent`  
**Location:** Single File at Root of Installation (`appsettings.json`)  
**Purpose:** Comprehensive reference for configuring server connections, timeouts, retries, security, and logging.

---

## 1. Complete `appsettings.json` Schema & Reference

```json
{
  "Agent": {
    "DisplayName": "GamingStation-01",
    "AutoStart": true,
    "RunInBackground": true,
    "Environment": "Production"
  },
  "Server": {
    "ServerIp": "192.168.1.100",
    "ApiPort": 5022,
    "WebSocketPort": 5001,
    "WebSocketPath": "/hubs/agent",
    "UseSecureConnection": false
  },
  "Connection": {
    "EnableSignalR": false,
    "HeartbeatIntervalSeconds": 15,
    "ConnectionTimeoutSeconds": 30,
    "ReconnectEnabled": true,
    "MaximumReconnectDelaySeconds": 60
  },
  "Security": {
    "RequireWss": true,
    "ValidateCertificates": true,
    "ExpectedCertificateThumbprint": "",
    "UseMachineScope": true,
    "RejectNonWss": false
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
  }
}
```

---

## 2. Setting Descriptions & Production Guidance

### `Server` Section (Primary Administrative Controls)
- **`ServerIp`**: Central server IP address or hostname (e.g. `"192.168.1.100"` or `"server.lan"`). Changing this single value updates both the REST registration and SignalR connection endpoints automatically.
- **`ApiPort`**: Backend REST API port (default: `5022`).
- **`WebSocketPort`**: SignalR WebSocket port (default: `5001`).
- **`WebSocketPath`**: SignalR Hub route (default: `"/hubs/agent"`).
- **`UseSecureConnection`**: When `true`, uses `https://` and `wss://`. When `false` (local LAN dev/test without SSL certs), uses `http://` and `ws://`.

### `Connection` Section
- **`EnableSignalR`**: Set to `false` while the central backend hub is not deployed. Set to `true` to activate real-time duplex communications.
- **`HeartbeatIntervalSeconds`**: Frequency of heartbeat dispatches (default: `15` seconds).
- **`ConnectionTimeoutSeconds`**: Socket connection timeout (default: `30` seconds).
- **`ReconnectEnabled`**: Enables automatic reconnection on disconnect (default: `true`).
- **`MaximumReconnectDelaySeconds`**: Reconnection backoff ceiling (default: `60` seconds).

### `Security` Section
- **`RequireWss`**: Enforces secure WebSockets (`wss://`).
- **`ValidateCertificates`**: Enforces strict TLS certificate expiry and CA validation (must be `true` in production).
- **`ExpectedCertificateThumbprint`**: Optional pinned SHA-1 thumbprint for self-signed cafe server certificates.
- **`UseMachineScope`**: Enables DPAPI `DataProtectionScope.LocalMachine` for reliable Windows Service persistence across boots.

### `Identity` Section
- **`StorageDirectory`**: Path where `device-identity.json` and credentials are saved. Relative paths are automatically resolved to the installation directory (`AppContext.BaseDirectory`).

---

## 3. How to Update Configuration on a Station

1. Stop the Windows Service:
   ```powershell
   .\Scripts\Stop-Agent.ps1
   ```
2. Edit `appsettings.json` using Notepad:
   ```powershell
   notepad.exe appsettings.json
   ```
3. Update `ServerIp` (e.g. `"192.168.1.50"`).
4. Save and restart the service:
   ```powershell
   .\Scripts\Start-Agent.ps1
   ```
5. Verify the service loaded the new IP:
   ```powershell
   .\Scripts\Get-AgentStatus.ps1
   ```
