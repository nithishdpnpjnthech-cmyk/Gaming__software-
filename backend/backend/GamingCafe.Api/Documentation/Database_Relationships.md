# Database Relationships Specification

## Foreign Key Constraints & Cardinality Verification

This document specifies the exact relationships, foreign key actions, cascade behaviors, and index implementations across the 17 database entities.

---

### Relationship Cardinality & Delete Action Matrix

| Foreign Key Constraint | Source Table & Column | Target Table & Column | Cardinality | Delete Behavior |
|---|---|---|---|---|
| `user_roles_user_id_fkey` | `user_roles.user_id` | `users.id` | N : 1 | `CASCADE` |
| `user_roles_role_id_fkey` | `user_roles.role_id` | `roles.id` | N : 1 | `RESTRICT` |
| `role_permissions_role_id_fkey` | `role_permissions.role_id` | `roles.id` | N : 1 | `CASCADE` |
| `role_permissions_permission_id_fkey` | `role_permissions.permission_id` | `permissions.id` | N : 1 | `CASCADE` |
| `audit_logs_user_id_fkey` | `audit_logs.user_id` | `users.id` | N : 1 | `SET NULL` |
| `system_settings_updated_by_fkey` | `system_settings.updated_by` | `users.id` | N : 1 | `SET NULL` |
| `devices_approved_by_fkey` | `devices.approved_by` | `users.id` | N : 1 | `SET NULL` |
| `device_status_device_id_fkey` | `device_status.device_id` | `devices.id` | 1 : 1 | `CASCADE` |
| `device_credentials_device_id_fkey` | `device_credentials.device_id` | `devices.id` | N : 1 | `CASCADE` |
| `device_heartbeats_device_id_fkey` | `device_heartbeats.device_id` | `devices.id` | N : 1 | `CASCADE` |
| `device_connection_logs_device_id_fkey` | `device_connection_logs.device_id` | `devices.id` | N : 1 | `CASCADE` |
| `device_network_interfaces_device_id_fkey` | `device_network_interfaces.device_id` | `devices.id` | N : 1 | `CASCADE` |
| `device_commands_device_id_fkey` | `device_commands.device_id` | `devices.id` | N : 1 | `RESTRICT` |
| `device_commands_requested_by_fkey` | `device_commands.requested_by` | `users.id` | N : 1 | `RESTRICT` |
| `device_command_logs_command_id_fkey` | `device_command_logs.command_id` | `device_commands.id` | N : 1 | `CASCADE` |
| `agent_logs_device_id_fkey` | `agent_logs.device_id` | `devices.id` | N : 1 | `CASCADE` |
| `security_events_device_id_fkey` | `security_events.device_id` | `devices.id` | N : 1 | `SET NULL` |

---

### Integrity & Safety Rules
1. **Station Command Protection**:
   - `device_commands` utilizes `ON DELETE RESTRICT` for `device_id` and `requested_by` so audit records of hardware/software execution commands cannot be accidentally wiped.
2. **Cascading Child Entities**:
   - `device_status`, `device_credentials`, `device_heartbeats`, `device_connection_logs`, and `agent_logs` cascade delete when a device record is explicitly purged by a super administrator.
3. **Auditor Anonymization**:
   - `audit_logs`, `security_events`, and `system_settings` use `ON DELETE SET NULL` on user deletion to preserve complete chronological activity records while removing the specific user reference.
