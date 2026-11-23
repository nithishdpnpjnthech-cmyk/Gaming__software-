# Gaming Cafe Client Agent — System Architecture

## 1. High-Level Design

The Gaming Cafe Client Agent operates as an autonomous, event-driven background service. It establishes an outbound-only WebSocket connection to the central server, eliminating the need to expose inbound network ports on gaming PCs.

```mermaid
flowchart TB
    subgraph ClientHost ["Gaming PC (Windows 10/11)"]
        subgraph SCM ["Windows Service Manager"]
            SvcProc["GamingCafe.ClientAgent.exe (NT AUTHORITY\\SYSTEM)"]
        end

        subgraph CoreEngine ["Agent Runtime Engine"]
            Worker["ClientAgentWorker (BackgroundService)"]
            ConfigSvc["ConfigurationService"]
            SecVal["SecurityValidationService"]
            ConnMgr["SignalRConnectionManager"]
            ConnState["ConnectionStateManager"]
            Reconn["ReconnectionService (Exp. Backoff + Jitter)"]
            MsgHandler["MessageHandler (Sliding De-duplication)"]
            EnrollSvc["DeviceEnrollmentService (8-State Machine)"]
            IdSvc["DeviceIdentityService"]
            AuthSvc["DeviceAuthenticationService"]
            HbSvc["HeartbeatService"]
            HealthSvc["AgentHealthService"]
            LogSvc["StructuredLogging & FileLoggerProvider"]
        end

        subgraph LocalDisk ["Secure Storage (%ProgramData%)"]
            IdFile[("device-identity.json")]
            CredFile[("device-credentials.dat (DPAPI)")]
            LogFile[("logs/clientagent-YYYY-MM-DD.log")]
        end
    end

    subgraph CentralServer ["Central Cafe Server"]
        Hub["SignalR AgentHub (Port 5001 WSS)"]
        AdminUI["Admin Management Console"]
    end

    SvcProc --> Worker
    Worker --> ConfigSvc
    Worker --> ConnMgr
    Worker --> EnrollSvc
    Worker --> HbSvc

    ConnMgr --> SecVal
    ConnMgr --> Reconn
    ConnMgr --> ConnState
    ConnMgr --> MsgHandler
    ConnMgr --> AuthSvc

    EnrollSvc --> IdSvc
    EnrollSvc --> AuthSvc
    AuthSvc --> CredFile
    IdSvc --> IdFile

    HbSvc --> HealthSvc
    HbSvc --> ConnMgr

    LogSvc --> LogFile
    ConnMgr <== "WSS / TLS 1.3" ==> Hub
    Hub <==> AdminUI
```

---

## 2. Component Interaction & Sequence Flow

### Startup and Enrollment Sequence

```mermaid
sequenceDiagram
    autonumber
    participant W as Worker
    participant ID as DeviceIdentity
    participant ES as EnrollmentService
    participant CM as ConnectionManager
    participant HUB as Central Backend Hub

    W->>ID: GetOrCreateIdentity()
    ID-->>W: UUID + Hardware Fingerprint
    W->>ES: InitializeStateAsync()
    ES-->>W: State = Unregistered (or Approved/Authenticated)
    W->>CM: StartAsync() [Enforcing WSS]
    CM->>HUB: WebSocket Upgrade Handshake (TLS)
    HUB-->>CM: Connection Established
    W->>CM: SendAsync("RegisterDevice", DeviceRegistrationMessage)
    CM->>HUB: Dispatch Registration Payload
    HUB-->>CM: Callback: DeviceApproval(IsApproved=true, Zone="VIP")
    CM->>ES: HandleApprovalMessageAsync()
    ES->>ES: State -> Approved
    HUB-->>CM: Callback: DeviceAuthentication(Token="JWT...")
    CM->>ES: HandleAuthenticationMessageAsync()
    ES->>ES: State -> Authenticated (DPAPI Persisted)
    loop Every 15 Seconds
        W->>CM: SendAsync("SendHeartbeat", HeartbeatMessage)
        CM->>HUB: Heartbeat Payload
        HUB-->>CM: Callback: HeartbeatAck(Sequence, Latency)
    end
```

---

## 3. Resilience and Self-Healing Strategy

1. **Connection Loss**:
   When the connection drops, `SignalRConnectionManager` delegates to `ReconnectionService`. Reconnection attempts are spaced with exponential backoff:
   $$\text{Delay} = \min(\text{MaxDelay}, \text{InitialDelay} \times 2^{\text{attempt}-1}) + \text{crypto\_jitter}$$
2. **Process Failure**:
   Configured in Windows Service Control Manager with recovery actions:
   - 1st Failure: Restart service after 5,000 ms.
   - 2nd Failure: Restart service after 10,000 ms.
   - Subsequent Failures: Restart service after 30,000 ms (reset period: 86,400s).
3. **Storage Corruption**:
   `DeviceIdentityStore` writes through temporary staging files (`.tmp.<guid>`) and verifies deserialization. Any corrupted file is automatically quarantined (`.corrupted.<timestamp>`) and replaced with a clean GUID identity.
