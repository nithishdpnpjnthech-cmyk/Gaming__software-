# Device Authentication Architecture

## Architecture Overview
The backend enforces strong zero-trust device authentication for all gaming stations connecting over the LAN.

---

### Authentication Flow
```
[Client Agent]                                        [Backend API]
      │                                                     │
      ├─── 1. POST /api/devices/{id}/status ───────────────>│ (Poll approval status)
      │<── 2. status: "APPROVED" ───────────────────────────┤
      │                                                     │
      ├─── 3. POST /api/devices/{id}/claim-secret ─────────>│ (Ownership check: localDeviceId)
      │<── 4. enrollmentSecret: "<RandomHex32>" ────────────┤
      │                                                     │
      │   (Agent encrypts secret via Windows DPAPI)         │
      │                                                     │
      ├─── 5. POST /api/auth/device/dev-login ─────────────>│
      │       { deviceId, enrollmentSecret }                │
      │                                                     ├── Validates device is APPROVED
      │                                                     ├── Hashes secret (SHA-256)
      │                                                     ├── Constant-time compare with secret_hash
      │                                                     └── Creates JWT (Claim: device_id)
      │<── 6. 200 OK: { accessToken, expiresAt } ───────────┤
      │                                                     │
      ├─── 7. Connect to /hubs/agent?access_token=<JWT> ───>│
      │<── 8. WebSocket connected (is_online = true) ───────┤
```

---

### Security Guarantees
1. **No Plaintext Secret Storage**:
   - `device_credentials.secret_hash` stores only 64-character lowercase hexadecimal SHA-256 hashes.
2. **Side-Channel Attack Prevention**:
   - Compares secret hashes using `CryptographicOperations.FixedTimeEquals` to prevent execution timing attacks.
3. **Short-Lived JWT Tokens**:
   - Access tokens expire within 60 minutes and are proactively refreshed by the client agent 3 minutes prior to expiration.
4. **Claim-Based Authorization**:
   - SignalR hub verifies the custom claim `device_id` issued in the JWT. Untrusted client payloads cannot spoof station identities.
