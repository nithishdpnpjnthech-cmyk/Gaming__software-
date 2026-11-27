# Model and Property Mapping

## Mapping Integrity Verification

| C# Property | Database Column | Data Type | Nullable | Notes / Purpose |
|---|---|---|---|---|
| `Device.Id` | `id` | `uuid` | NO | Primary key generated on backend (`gen_random_uuid()`) |
| `Device.LocalDeviceId` | `local_device_id` | `uuid` | YES | Unique client-generated GUID for idempotent retries |
| `Device.DeviceCode` | `device_code` | `varchar(50)` | NO | Unique station code (`PC-XXXXXXXX`) |
| `Device.DisplayName` | `display_name` | `varchar(100)` | NO | Station label |
| `Device.RegistrationStatus` | `registration_status` | `varchar(20)` | NO | `PENDING`, `APPROVED`, `REJECTED`, `REVOKED` |
| `DeviceCredential.SecretHash`| `secret_hash` | `varchar(128)` | YES | SHA-256 hash of device enrollment secret |
| `DeviceCredential.CertificateThumbprint` | `certificate_thumbprint` | `varchar(128)` | YES | Client certificate hash (for mTLS optional mode) |
| `DeviceStatus.IsOnline` | `is_online` | `boolean` | NO | Live operational status used by offline monitor |
| `DeviceStatus.LastSeenAt` | `last_seen_at` | `timestamptz` | YES | Updated on heartbeat or connection |
| `DeviceHeartbeat.ReceivedAt` | `received_at` | `timestamptz` | NO | Monotonic server UTC reception timestamp |
| `DeviceConnectionLog.EventType`| `event_type` | `varchar(30)` | NO | `CONNECTED`, `DISCONNECTED`, `TIMEOUT` |

## Fluent API Mapping Alignment
All model classes in `Models/` precisely map to their corresponding PostgreSQL tables and snake_case column names via Fluent API in `GamingCafeDbContext.OnModelCreating`.
