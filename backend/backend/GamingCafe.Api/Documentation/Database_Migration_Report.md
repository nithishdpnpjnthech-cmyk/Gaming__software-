# Database Migration Report

## Migration State & Applied Changes
The backend schema was mapped and validated against the authoritative PostgreSQL database `gamingcafe1`.

### Applied Schema Migrations:
1. **`local_device_id` on `public.devices`**:
   - Column: `local_device_id uuid NULL`
   - Index: `idx_devices_local_device_id` UNIQUE (`local_device_id WHERE local_device_id IS NOT NULL`)
   - Purpose: Enables idempotent client registration retries without primary key collision.

2. **`secret_hash` on `public.device_credentials`**:
   - Column: `secret_hash varchar(128) NULL`
   - Modified: `certificate_thumbprint` altered from NOT NULL to NULLABLE.
   - Purpose: Supports zero-trust LAN secret provisioning and SHA-256 constant-time authentication.

3. **`is_online` on `public.device_status`**:
   - Column: `is_online boolean NOT NULL DEFAULT false`
   - Purpose: Powers real-time online/offline indicators for `DeviceOfflineMonitorService` and `AgentHub`.

### Rollback Commands (if needed)
```sql
ALTER TABLE public.devices DROP COLUMN IF EXISTS local_device_id;
ALTER TABLE public.device_credentials DROP COLUMN IF EXISTS secret_hash;
ALTER TABLE public.device_status DROP COLUMN IF EXISTS is_online;
```
