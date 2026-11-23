-- Gaming Cafe PC Management System - PostgreSQL schema v1.0
-- Proposed design; review with backend owner before applying.
BEGIN;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE roles (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 code varchar(50) NOT NULL UNIQUE,
 name varchar(100) NOT NULL,
 is_system_role boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE permissions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 code varchar(100) NOT NULL UNIQUE,
 module varchar(50) NOT NULL,
 action varchar(50) NOT NULL,
 description text,
 UNIQUE(module, action)
);
CREATE TABLE users (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 username varchar(100) NOT NULL UNIQUE,
 email varchar(255) UNIQUE,
 password_hash text NOT NULL,
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE user_roles (
 user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
 role_id uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
 assigned_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(user_id, role_id)
);
CREATE TABLE role_permissions (
 role_id uuid NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
 permission_id uuid NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
 PRIMARY KEY(role_id, permission_id)
);
CREATE TABLE devices (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 device_code varchar(50) NOT NULL UNIQUE,
 display_name varchar(100) NOT NULL,
 hostname varchar(255),
 hardware_fingerprint_hash varchar(128),
 agent_version varchar(30),
 registration_status varchar(20) NOT NULL DEFAULT 'PENDING' CHECK (registration_status IN ('PENDING','APPROVED','REJECTED','REVOKED')),
 approved_by uuid REFERENCES users(id) ON DELETE SET NULL,
 approved_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE device_status (
 device_id uuid PRIMARY KEY REFERENCES devices(id) ON DELETE CASCADE,
 last_seen_at timestamptz,
 last_ip_address inet,
 reported_power_state varchar(20) CHECK (reported_power_state IN ('ON','SLEEP','HIBERNATE','SHUTDOWN','UNKNOWN')),
 last_status_change_at timestamptz,
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE device_network_interfaces (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
 interface_name varchar(100),
 mac_address macaddr NOT NULL,
 ip_address inet,
 subnet_cidr cidr,
 is_primary boolean NOT NULL DEFAULT false,
 wol_enabled boolean NOT NULL DEFAULT false,
 wol_supported boolean,
 last_reported_at timestamptz,
 UNIQUE(device_id, mac_address)
);
CREATE UNIQUE INDEX uq_device_primary_interface ON device_network_interfaces(device_id) WHERE is_primary;
CREATE TABLE device_credentials (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
 certificate_thumbprint varchar(128) NOT NULL UNIQUE,
 certificate_serial_number varchar(128),
 certificate_subject text,
 credential_status varchar(20) NOT NULL DEFAULT 'PENDING' CHECK (credential_status IN ('PENDING','ACTIVE','REVOKED','EXPIRED')),
 issued_at timestamptz,
 expires_at timestamptz,
 revoked_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE security_events (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 device_id uuid REFERENCES devices(id) ON DELETE SET NULL,
 event_type varchar(60) NOT NULL,
 outcome varchar(20) NOT NULL CHECK (outcome IN ('SUCCESS','FAILURE','DENIED')),
 details text,
 occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE device_heartbeats (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
 received_at timestamptz NOT NULL DEFAULT now(),
 agent_version varchar(30),
 source_ip inet
);
CREATE TABLE device_connection_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
 event_type varchar(30) NOT NULL CHECK (event_type IN ('CONNECTED','DISCONNECTED','TIMEOUT','RECONNECTED')),
 event_time timestamptz NOT NULL DEFAULT now(),
 details text
);
CREATE TABLE device_commands (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE RESTRICT,
 requested_by uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
 command_type varchar(30) NOT NULL CHECK (command_type IN ('WAKE','SHUTDOWN','RESTART','SLEEP','HIBERNATE','LOCK','GET_STATUS')),
 command_status varchar(30) NOT NULL DEFAULT 'PENDING' CHECK (command_status IN ('PENDING','DISPATCHED','ACKNOWLEDGED','SUCCEEDED','FAILED','EXPIRED','CANCELLED')),
 reason text,
 requested_at timestamptz NOT NULL DEFAULT now(),
 expires_at timestamptz NOT NULL,
 dispatched_at timestamptz,
 completed_at timestamptz,
 error_message text,
 CHECK (expires_at > requested_at)
);
CREATE TABLE device_command_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 command_id uuid NOT NULL REFERENCES device_commands(id) ON DELETE CASCADE,
 event_type varchar(30) NOT NULL CHECK (event_type IN ('CREATED','DISPATCHED','ACKNOWLEDGED','EXECUTING','SUCCEEDED','FAILED','EXPIRED','CANCELLED')),
 details text,
 event_time timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE agent_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id uuid NOT NULL UNIQUE,
 device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
 log_level varchar(20) NOT NULL CHECK (log_level IN ('Verbose','Debug','Information','Warning','Error','Fatal')),
 event_type varchar(50) NOT NULL,
 message text NOT NULL,
 exception_details text,
 correlation_id uuid,
 occurred_at timestamptz NOT NULL,
 received_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE audit_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 user_id uuid REFERENCES users(id) ON DELETE SET NULL,
 action varchar(100) NOT NULL,
 target_type varchar(50),
 target_id uuid,
 details jsonb,
 occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE system_settings (
 setting_key varchar(100) PRIMARY KEY,
 setting_value jsonb NOT NULL,
 updated_by uuid REFERENCES users(id) ON DELETE SET NULL,
 updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_device_heartbeats_device_time ON device_heartbeats(device_id, received_at DESC);
CREATE INDEX idx_device_connections_device_time ON device_connection_logs(device_id, event_time DESC);
CREATE INDEX idx_device_commands_device_status ON device_commands(device_id, command_status, requested_at DESC);
CREATE INDEX idx_device_command_logs_command_time ON device_command_logs(command_id, event_time DESC);
CREATE INDEX idx_agent_logs_device_time ON agent_logs(device_id, occurred_at DESC);
CREATE INDEX idx_security_events_device_time ON security_events(device_id, occurred_at DESC);
CREATE INDEX idx_audit_logs_user_time ON audit_logs(user_id, occurred_at DESC);
CREATE INDEX idx_device_credentials_device ON device_credentials(device_id);
CREATE INDEX idx_user_roles_role ON user_roles(role_id);
CREATE INDEX idx_role_permissions_permission ON role_permissions(permission_id);

INSERT INTO roles(code,name,is_system_role) VALUES
 ('SUPER_ADMIN','Super Admin',true),
 ('ADMIN','Admin',true),
 ('STAFF','Staff',true);
INSERT INTO system_settings(setting_key,setting_value) VALUES
 ('heartbeat_interval_seconds','30'::jsonb),
 ('heartbeat_timeout_seconds','90'::jsonb);
COMMIT;
-- Bootstrap the first Super Admin securely through an approved setup workflow.
-- Seed granular permissions and role_permissions after agreeing on the API authorization matrix.
-- SUPER_ADMIN full access must be enforced server-side, including new permissions.
-- Device status ONLINE/OFFLINE is derived from authenticated last_seen_at + timeout.
-- WAKE is an Admin-side WoL action; other power commands require an online authenticated Agent.
