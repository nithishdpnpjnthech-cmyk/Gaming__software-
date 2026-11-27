# Database Entity Mapping Specification

## Exact C# Entity Model to PostgreSQL 18 Table Mappings

This document specifies the exact mapping between the 17 C# entity classes and the authoritative database tables depicted in the Gaming Cafe ER diagram.

---

### 1. `users` Table
- **C# Model**: `GamingCafe.Api.Models.User`
- **Table Name**: `public.users`
- **Primary Key**: `id` (`uuid`, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `Username` -> `username` (`varchar(100)`, NOT NULL, UNIQUE: `users_username_key`)
  - `Email` -> `email` (`varchar(255)`, NULLABLE, UNIQUE: `users_email_key`)
  - `PasswordHash` -> `password_hash` (`text`, NOT NULL)
  - `IsActive` -> `is_active` (`boolean`, NOT NULL, DEFAULT: `true`)
  - `CreatedAt` -> `created_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `UpdatedAt` -> `updated_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 2. `roles` Table
- **C# Model**: `GamingCafe.Api.Models.Role`
- **Table Name**: `public.roles`
- **Primary Key**: `id` (`uuid`, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `Code` -> `code` (`varchar(50)`, NOT NULL, UNIQUE: `roles_code_key`)
  - `Name` -> `name` (`varchar(100)`, NOT NULL)
  - `IsSystemRole` -> `is_system_role` (`boolean`, NOT NULL, DEFAULT: `false`)
  - `CreatedAt` -> `created_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 3. `permissions` Table
- **C# Model**: `GamingCafe.Api.Models.Permission`
- **Table Name**: `public.permissions`
- **Primary Key**: `id` (`uuid`, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `Code` -> `code` (`varchar(100)`, NOT NULL, UNIQUE: `permissions_code_key`)
  - `Module` -> `module` (`varchar(50)`, NOT NULL)
  - `Action` -> `action` (`varchar(50)`, NOT NULL)
  - `Description` -> `description` (`text`, NULLABLE)
- **Composite Unique Index**: `(module, action)` -> `permissions_module_action_key`

---

### 4. `user_roles` Table
- **C# Model**: `GamingCafe.Api.Models.UserRole`
- **Table Name**: `public.user_roles`
- **Composite Primary Key**: `(user_id, role_id)` -> `user_roles_pkey`
- **Properties**:
  - `UserId` -> `user_id` (`uuid`, NOT NULL, FK -> `users.id`)
  - `RoleId` -> `role_id` (`uuid`, NOT NULL, FK -> `roles.id`)
  - `AssignedAt` -> `assigned_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 5. `role_permissions` Table
- **C# Mapping**: Many-to-many join entity via EF Core `.UsingEntity` in `GamingCafeDbContext`
- **Table Name**: `public.role_permissions`
- **Composite Primary Key**: `(role_id, permission_id)` -> `role_permissions_pkey`
- **Properties**:
  - `role_id` (`uuid`, NOT NULL, FK -> `roles.id`)
  - `permission_id` (`uuid`, NOT NULL, FK -> `permissions.id`)

---

### 6. `audit_logs` Table
- **C# Model**: `GamingCafe.Api.Models.AuditLog`
- **Table Name**: `public.audit_logs`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `UserId` -> `user_id` (`uuid`, NULLABLE, FK -> `users.id`)
  - `Action` -> `action` (`varchar(100)`, NOT NULL)
  - `TargetType` -> `target_type` (`varchar(50)`, NULLABLE)
  - `TargetId` -> `target_id` (`uuid`, NULLABLE)
  - `Details` -> `details` (`jsonb`, NULLABLE)
  - `OccurredAt` -> `occurred_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 7. `system_settings` Table
- **C# Model**: `GamingCafe.Api.Models.SystemSetting`
- **Table Name**: `public.system_settings`
- **Primary Key**: `setting_key` (`varchar(100)`)
- **Properties**:
  - `SettingKey` -> `setting_key` (`varchar(100)`, NOT NULL)
  - `SettingValue` -> `setting_value` (`jsonb`, NOT NULL)
  - `UpdatedBy` -> `updated_by` (`uuid`, NULLABLE, FK -> `users.id`)
  - `UpdatedAt` -> `updated_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 8. `devices` Table
- **C# Model**: `GamingCafe.Api.Models.Device`
- **Table Name**: `public.devices`
- **Primary Key**: `id` (`uuid`, NOT NULL, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `DeviceCode` -> `device_code` (`varchar(50)`, NOT NULL, UNIQUE: `devices_device_code_key`)
  - `DisplayName` -> `display_name` (`varchar(100)`, NOT NULL)
  - `Hostname` -> `hostname` (`varchar(255)`, NULLABLE)
  - `HardwareFingerprintHash` -> `hardware_fingerprint_hash` (`varchar(128)`, NULLABLE)
  - `AgentVersion` -> `agent_version` (`varchar(30)`, NULLABLE)
  - `RegistrationStatus` -> `registration_status` (`varchar(20)`, NOT NULL, DEFAULT: `'PENDING'`)
  - `ApprovedBy` -> `approved_by` (`uuid`, NULLABLE, FK -> `users.id`)
  - `ApprovedAt` -> `approved_at` (`timestamptz`, NULLABLE)
  - `CreatedAt` -> `created_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `UpdatedAt` -> `updated_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `LocalDeviceId` -> `local_device_id` (`uuid`, NULLABLE, UNIQUE: `idx_devices_local_device_id`)

---

### 9. `device_credentials` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceCredential`
- **Table Name**: `public.device_credentials`
- **Primary Key**: `id` (`uuid`, NOT NULL, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
  - `CertificateThumbprint` -> `certificate_thumbprint` (`varchar(128)`, NULLABLE, UNIQUE)
  - `CertificateSerialNumber` -> `certificate_serial_number` (`varchar(128)`, NULLABLE)
  - `CertificateSubject` -> `certificate_subject` (`text`, NULLABLE)
  - `CredentialStatus` -> `credential_status` (`varchar(20)`, NOT NULL, DEFAULT: `'PENDING'`)
  - `IssuedAt` -> `issued_at` (`timestamptz`, NULLABLE)
  - `ExpiresAt` -> `expires_at` (`timestamptz`, NULLABLE)
  - `RevokedAt` -> `revoked_at` (`timestamptz`, NULLABLE)
  - `CreatedAt` -> `created_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `SecretHash` -> `secret_hash` (`varchar(128)`, NULLABLE)

---

### 10. `device_status` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceStatus`
- **Table Name**: `public.device_status`
- **Primary Key**: `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
- **Properties**:
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL)
  - `LastSeenAt` -> `last_seen_at` (`timestamptz`, NULLABLE)
  - `LastIpAddress` -> `last_ip_address` (`inet`, NULLABLE)
  - `ReportedPowerState` -> `reported_power_state` (`varchar(20)`, NULLABLE)
  - `IsOnline` -> `is_online` (`boolean`, NOT NULL, DEFAULT: `false`)
  - `LastStatusChangeAt` -> `last_status_change_at` (`timestamptz`, NULLABLE)
  - `UpdatedAt` -> `updated_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 11. `device_network_interfaces` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceNetworkInterface`
- **Table Name**: `public.device_network_interfaces`
- **Primary Key**: `id` (`uuid`, NOT NULL, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
  - `InterfaceName` -> `interface_name` (`varchar(100)`, NOT NULL)
  - `MacAddress` -> `mac_address` (`macaddr`, NOT NULL)
  - `IpAddress` -> `ip_address` (`inet`, NULLABLE)
  - `SubnetCidr` -> `subnet_cidr` (`cidr`, NULLABLE)
  - `IsPrimary` -> `is_primary` (`boolean`, NOT NULL, DEFAULT: `false`)
  - `WolEnabled` -> `wol_enabled` (`boolean`, NOT NULL, DEFAULT: `false`)
  - `WolSupported` -> `wol_supported` (`boolean`, NOT NULL, DEFAULT: `false`)
  - `LastReportedAt` -> `last_reported_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 12. `device_heartbeats` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceHeartbeat`
- **Table Name**: `public.device_heartbeats`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
  - `ReceivedAt` -> `received_at` (`timestamptz`, NOT NULL)
  - `AgentVersion` -> `agent_version` (`varchar(30)`, NULLABLE)
  - `SourceIp` -> `source_ip` (`inet`, NULLABLE)

---

### 13. `device_connection_logs` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceConnectionLog`
- **Table Name**: `public.device_connection_logs`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
  - `EventType` -> `event_type` (`varchar(30)`, NOT NULL)
  - `EventTime` -> `event_time` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `Details` -> `details` (`text`, NULLABLE)

---

### 14. `agent_logs` Table
- **C# Model**: `GamingCafe.Api.Models.AgentLog`
- **Table Name**: `public.agent_logs`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `EventId` -> `event_id` (`uuid`, NOT NULL, UNIQUE)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE CASCADE)
  - `LogLevel` -> `log_level` (`varchar(20)`, NOT NULL)
  - `EventType` -> `event_type` (`varchar(50)`, NOT NULL)
  - `Message` -> `message` (`text`, NOT NULL)
  - `ExceptionDetails` -> `exception_details` (`text`, NULLABLE)
  - `CorrelationId` -> `correlation_id` (`uuid`, NULLABLE)
  - `OccurredAt` -> `occurred_at` (`timestamptz`, NOT NULL)
  - `ReceivedAt` -> `received_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 15. `device_commands` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceCommand`
- **Table Name**: `public.device_commands`
- **Primary Key**: `id` (`uuid`, NOT NULL, `gen_random_uuid()`)
- **Properties**:
  - `Id` -> `id` (`uuid`, NOT NULL)
  - `DeviceId` -> `device_id` (`uuid`, NOT NULL, FK -> `devices.id` ON DELETE RESTRICT)
  - `RequestedBy` -> `requested_by` (`uuid`, NOT NULL, FK -> `users.id`)
  - `CommandType` -> `command_type` (`varchar(30)`, NOT NULL)
  - `CommandStatus` -> `command_status` (`varchar(30)`, NOT NULL)
  - `Reason` -> `reason` (`text`, NULLABLE)
  - `RequestedAt` -> `requested_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
  - `ExpiresAt` -> `expires_at` (`timestamptz`, NOT NULL)
  - `DispatchedAt` -> `dispatched_at` (`timestamptz`, NULLABLE)
  - `CompletedAt` -> `completed_at` (`timestamptz`, NULLABLE)
  - `ErrorMessage` -> `error_message` (`text`, NULLABLE)

---

### 16. `device_command_logs` Table
- **C# Model**: `GamingCafe.Api.Models.DeviceCommandLog`
- **Table Name**: `public.device_command_logs`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `CommandId` -> `command_id` (`uuid`, NOT NULL, FK -> `device_commands.id` ON DELETE CASCADE)
  - `EventType` -> `event_type` (`varchar(30)`, NOT NULL)
  - `Details` -> `details` (`text`, NULLABLE)
  - `EventTime` -> `event_time` (`timestamptz`, NOT NULL, DEFAULT: `now()`)

---

### 17. `security_events` Table
- **C# Model**: `GamingCafe.Api.Models.SecurityEvent`
- **Table Name**: `public.security_events`
- **Primary Key**: `id` (`bigint IDENTITY`)
- **Properties**:
  - `Id` -> `id` (`bigint`, NOT NULL, `UseIdentityAlwaysColumn()`)
  - `DeviceId` -> `device_id` (`uuid`, NULLABLE, FK -> `devices.id` ON DELETE SET NULL)
  - `EventType` -> `event_type` (`varchar(60)`, NOT NULL)
  - `Outcome` -> `outcome` (`varchar(20)`, NOT NULL)
  - `Details` -> `details` (`text`, NULLABLE)
  - `OccurredAt` -> `occurred_at` (`timestamptz`, NOT NULL, DEFAULT: `now()`)
