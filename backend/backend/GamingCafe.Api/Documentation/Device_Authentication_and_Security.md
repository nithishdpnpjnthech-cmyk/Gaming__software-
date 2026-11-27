# Device Authentication and Security

## Zero-Trust Device Security Pipeline
1. **Administrative Approval**:
   - Device registrations start strictly in `PENDING` status.
   - Devices cannot authenticate or establish SignalR connections until approved by an administrator via `POST /api/devices/{id}/approve`.
2. **Dynamic Random Secret**:
   - Backend generates a cryptographically secure 256-bit random secret upon approval.
   - Secret SHA-256 hash is stored in `device_credentials.secret_hash`.
3. **Constant-Time Verification**:
   - Secret hashes are checked using `CryptographicOperations.FixedTimeEquals` to prevent side-channel timing attacks.
4. **JWT Device Token Issuance**:
   - `JwtTokenService` generates HMAC-SHA256 tokens signed with `Jwt:SecretKey`.
   - Embeds claim `"device_id" = "<BackendDeviceId>"`.
   - Token expires in 60 minutes and is refreshed automatically by active agents.
