# Gaming Cafe Client Agent — Enrollment & Backend API Contract

## Overview
Device enrollment is the handshake mechanism through which a new or wiped Gaming PC establishes trust with the central Admin PC backend. Until approved by a cafe administrator in the management console, the PC remains in a pending state and cannot access gaming sessions or protected operations.

---

## Enrollment Lifecycle & State Machine

```
              +----------------------+
              |     Unregistered     |
              +----------+-----------+
                         |
                         | Send Registration Request (POST /devices/register)
                         v
              +----------------------+
              |  RegistrationPending |
              +----------+-----------+
                         |
                         | Backend returns PendingApproval
                         v
              +----------------------+
              |   PendingApproval    |<-------------+
              +----+------------+----+              |
                   |            |                   | Status poll returns
   Admin Approves  |            |  Admin Rejects    | Pending
                   v            v                   |
            +------------+  +------------+          |
            |  Approved  |  |  Rejected  |          |
            +-----+------+  +------------+          |
                  |                                 |
                  | Exchange / Save Secret          |
                  v                                 |
           +---------------+                        |
           | Authenticated |                        |
           +-------+-------+                        |
                   |                                |
       SignalR WSS | Revocation / 401 Unauthorized  |
       Loop Active v                                |
            +------------+                          |
            |  Revoked   |--------------------------+
            +------------+
```

---

## State Definitions

| State | Description | Permitted Actions |
| :--- | :--- | :--- |
| **Unregistered** | First initialization; identity generated but not yet communicated to backend. | Can call `POST /api/v1/devices/register`. |
| **RegistrationPending** | Initial registration in flight. | Awaiting response. |
| **PendingApproval** | Registration received by backend; awaiting cafe admin review in dashboard. | Can poll `GET /api/v1/devices/{deviceId}/status` with backoff. **No heartbeats**. |
| **Approved** | Administrator approved the device; token available for secure retrieval. | Agent transitions to storing credential. |
| **Authenticated** | Device holds valid DPAPI-encrypted credential. | Permitted to establish SignalR WSS connection and dispatch periodic heartbeats. |
| **Rejected** | Device enrollment rejected by admin. | Credentials wiped; operations halted. |
| **Revoked** | Previously approved device was revoked. | Credentials wiped; operations halted. |
| **Error** | Temporary network or server error during registration. | Backs off safely. |
| **Retrying** | Backoff delay active prior to retry attempt. | Exponential backoff with cryptographic jitter. |

---

## Proposed API Contract (Awaiting Backend Finalization)

> **Important**: The backend API has not yet been finalized. The endpoints and schemas below represent the concrete proposal implemented in `IBackendApiClient`.

### 1. Device Registration
- **Endpoint**: `POST /api/v1/devices/register`
- **Authentication**: None (Initial enrollment request)
- **Request Payload**:
  ```json
  {
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "hostname": "GAMING-PC-01",
    "operatingSystem": "Microsoft Windows 10.0.26300",
    "agentVersion": "1.0.0-phase1",
    "clientTimestampUtc": "2026-11-20T14:30:00Z",
    "hardwareDigest": "A1B2C3D4E5F6..."
  }
  ```
- **Response Payload (`200 OK` or `202 Accepted`)**:
  ```json
  {
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "PendingApproval",
    "deviceSecret": null,
    "message": "Device registration received. Awaiting admin approval.",
    "retryAfterSeconds": 10
  }
  ```

### 2. Enrollment Status Polling
- **Endpoint**: `GET /api/v1/devices/{deviceId}/status`
- **Authentication**: None
- **Response Payload (`200 OK`)**:
  ```json
  {
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Approved",
    "deviceSecret": "sec_jwt_or_cryptographic_token_xyz",
    "message": "Device approved by administrator.",
    "retryAfterSeconds": null
  }
  ```

### 3. Fallback Heartbeat (REST)
- **Endpoint**: `POST /api/v1/devices/heartbeat`
- **Headers**: `Authorization: Bearer <deviceSecret>`
- **Request Payload**:
  ```json
  {
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "hostname": "GAMING-PC-01",
    "agentVersion": "1.0.0-phase1",
    "protocolVersion": "1.0",
    "timestampUtc": "2026-11-20T14:35:00Z",
    "agentStatus": "Running",
    "serviceUptimeSeconds": 300.5,
    "connectionTransport": "REST",
    "processMemoryBytes": 45120000,
    "lastCommunicationUtc": "2026-11-20T14:34:00Z"
  }
  ```

---

## Action Items for Backend Team Alignment
1. **Confirm Endpoint Routes**: Agree on URL path prefixes (e.g. `/api/v1/devices/...` vs `/api/devices/...`).
2. **Confirm Token Format**: Decide whether `deviceSecret` is an opaque high-entropy API key or a signed JWT.
3. **Confirm Hub Method Names**: Confirm SignalR method name `SendHeartbeat` and client handler `EnrollmentStatusChanged`.
