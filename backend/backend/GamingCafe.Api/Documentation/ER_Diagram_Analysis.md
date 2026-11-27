# ER Diagram Analysis

## Authoritative ER Diagram Specifications
The provided ER diagram serves as the authoritative database design reference. It encompasses 17 distinct relational tables structured for gaming cafe operations, access control, station telemetry, and auditability.

## Entity Mapping Table

| ER Diagram Entity | C# Entity Model | PostgreSQL Table | Primary Key | Key Relationships / Foreign Keys |
|---|---|---|---|---|
| `users` | `User` | `users` | `id (uuid)` | Referenced by `user_roles`, `audit_logs`, `devices.approved_by` |
| `roles` | `Role` | `roles` | `id (uuid)` | Referenced by `user_roles`, `role_permissions` |
| `user_roles` | `UserRole` | `user_roles` | `(user_id, role_id)` | FK -> `users(id)`, FK -> `roles(id)` |
| `permissions` | `Permission` | `permissions` | `id (uuid)` | Referenced by `role_permissions` |
| `role_permissions` | Join Table | `role_permissions` | `(role_id, permission_id)` | FK -> `roles(id)`, FK -> `permissions(id)` |
| `system_settings` | `SystemSetting` | `system_settings` | `setting_key (varchar)` | FK -> `users(id)` (updated_by) |
| `audit_logs` | `AuditLog` | `audit_logs` | `id (bigint IDENTITY)` | FK -> `users(id)` |
| `devices` | `Device` | `devices` | `id (uuid)` | FK -> `users(id)` (approved_by) |
| `device_status` | `DeviceStatus` | `device_status` | `device_id (uuid)` | 1-to-1 FK -> `devices(id)` ON DELETE CASCADE |
| `device_credentials` | `DeviceCredential` | `device_credentials`| `id (uuid)` | FK -> `devices(id)` ON DELETE CASCADE |
| `device_heartbeats` | `DeviceHeartbeat` | `device_heartbeats` | `id (bigint IDENTITY)` | FK -> `devices(id)` ON DELETE CASCADE |
| `device_connection_logs`| `DeviceConnectionLog` | `device_connection_logs` | `id (bigint IDENTITY)` | FK -> `devices(id)` ON DELETE CASCADE |
| `device_network_interfaces`| `DeviceNetworkInterface`| `device_network_interfaces`| `id (uuid)` | FK -> `devices(id)` ON DELETE CASCADE |
| `device_commands` | `DeviceCommand` | `device_commands` | `id (uuid)` | FK -> `devices(id)`, FK -> `users(id)` (requested_by) |
| `device_command_logs` | `DeviceCommandLog` | `device_command_logs` | `id (bigint IDENTITY)` | FK -> `device_commands(id)` |
| `agent_logs` | `AgentLog` | `agent_logs` | `id (bigint IDENTITY)` | FK -> `devices(id)` ON DELETE CASCADE |
| `security_events` | `SecurityEvent` | `security_events` | `id (bigint IDENTITY)` | FK -> `devices(id)` ON DELETE SET NULL |

## Schema Details
- Microsecond UTC timestamps (`timestamptz`) applied across all event tables.
- JSONB columns utilized in `system_settings.setting_value` and `audit_logs.details`.
- Network types (`inet`, `cidr`, `macaddr`) utilized in `device_network_interfaces` and `device_status`.
