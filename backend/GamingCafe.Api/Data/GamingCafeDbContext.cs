using System;
using System.Collections.Generic;
using GamingCafe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GamingCafe.Api.Data;


public partial class GamingCafeDbContext : DbContext
{
    public GamingCafeDbContext(DbContextOptions<GamingCafeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AgentLog> AgentLogs { get; set; } = null!;

    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;

    public virtual DbSet<Device> Devices { get; set; } = null!;

    public virtual DbSet<DeviceCommand> DeviceCommands { get; set; } = null!;

    public virtual DbSet<DeviceCommandLog> DeviceCommandLogs { get; set; } = null!;

    public virtual DbSet<DeviceConnectionLog> DeviceConnectionLogs { get; set; } = null!;

    public virtual DbSet<DeviceCredential> DeviceCredentials { get; set; } = null!;

    public virtual DbSet<DeviceHeartbeat> DeviceHeartbeats { get; set; } = null!;

    public virtual DbSet<DeviceNetworkInterface> DeviceNetworkInterfaces { get; set; } = null!;

    public virtual DbSet<DeviceStatus> DeviceStatuses { get; set; } = null!;

    public virtual DbSet<Permission> Permissions { get; set; } = null!;

    public virtual DbSet<Role> Roles { get; set; } = null!;

    public virtual DbSet<SecurityEvent> SecurityEvents { get; set; } = null!;

    public virtual DbSet<SystemSetting> SystemSettings { get; set; } = null!;

    public virtual DbSet<User> Users { get; set; } = null!;

    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<AgentLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("agent_logs_pkey");

            entity.ToTable("agent_logs");

            entity.HasIndex(e => e.EventId, "agent_logs_event_id_key").IsUnique();

            entity.HasIndex(e => new { e.DeviceId, e.OccurredAt }, "idx_agent_logs_device_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .HasColumnName("event_type");
            entity.Property(e => e.ExceptionDetails).HasColumnName("exception_details");
            entity.Property(e => e.LogLevel)
                .HasMaxLength(20)
                .HasColumnName("log_level");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("received_at");

            entity.HasOne(d => d.Device).WithMany(p => p.AgentLogs)
                .HasForeignKey(d => d.DeviceId)
                .HasConstraintName("agent_logs_device_id_fkey");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("audit_logs_pkey");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => new { e.UserId, e.OccurredAt }, "idx_audit_logs_user_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.Details)
                .HasColumnType("jsonb")
                .HasColumnName("details");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("occurred_at");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .HasColumnName("target_type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("devices_pkey");

            entity.ToTable("devices");

            entity.HasIndex(e => e.DeviceCode, "devices_device_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AgentVersion)
                .HasMaxLength(30)
                .HasColumnName("agent_version");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeviceCode)
                .HasMaxLength(50)
                .HasColumnName("device_code");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasColumnName("display_name");
            entity.Property(e => e.HardwareFingerprintHash)
                .HasMaxLength(128)
                .HasColumnName("hardware_fingerprint_hash");
            entity.Property(e => e.Hostname)
                .HasMaxLength(255)
                .HasColumnName("hostname");
            entity.Property(e => e.RegistrationStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("registration_status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.Devices)
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("devices_approved_by_fkey");
        });

        modelBuilder.Entity<DeviceCommand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_commands_pkey");

            entity.ToTable("device_commands");

            entity.HasIndex(e => new { e.DeviceId, e.CommandStatus, e.RequestedAt }, "idx_device_commands_device_status").IsDescending(false, false, true);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CommandStatus)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("command_status");
            entity.Property(e => e.CommandType)
                .HasMaxLength(30)
                .HasColumnName("command_type");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.DispatchedAt).HasColumnName("dispatched_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("requested_at");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");

            entity.HasOne(d => d.Device).WithMany(p => p.DeviceCommands)
                .HasForeignKey(d => d.DeviceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("device_commands_device_id_fkey");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.DeviceCommands)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("device_commands_requested_by_fkey");
        });

        modelBuilder.Entity<DeviceCommandLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_command_logs_pkey");

            entity.ToTable("device_command_logs");

            entity.HasIndex(e => new { e.CommandId, e.EventTime }, "idx_device_command_logs_command_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.CommandId).HasColumnName("command_id");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.EventTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("event_time");
            entity.Property(e => e.EventType)
                .HasMaxLength(30)
                .HasColumnName("event_type");

            entity.HasOne(d => d.Command).WithMany(p => p.DeviceCommandLogs)
                .HasForeignKey(d => d.CommandId)
                .HasConstraintName("device_command_logs_command_id_fkey");
        });

        modelBuilder.Entity<DeviceConnectionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_connection_logs_pkey");

            entity.ToTable("device_connection_logs");

            entity.HasIndex(e => new { e.DeviceId, e.EventTime }, "idx_device_connections_device_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.EventTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("event_time");
            entity.Property(e => e.EventType)
                .HasMaxLength(30)
                .HasColumnName("event_type");

            entity.HasOne(d => d.Device).WithMany(p => p.DeviceConnectionLogs)
                .HasForeignKey(d => d.DeviceId)
                .HasConstraintName("device_connection_logs_device_id_fkey");
        });

        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_credentials_pkey");

            entity.ToTable("device_credentials");

            entity.HasIndex(e => e.CertificateThumbprint, "device_credentials_certificate_thumbprint_key").IsUnique();

            entity.HasIndex(e => e.DeviceId, "idx_device_credentials_device");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CertificateSerialNumber)
                .HasMaxLength(128)
                .HasColumnName("certificate_serial_number");
            entity.Property(e => e.CertificateSubject).HasColumnName("certificate_subject");
            entity.Property(e => e.CertificateThumbprint)
                .HasMaxLength(128)
                .HasColumnName("certificate_thumbprint");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CredentialStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("credential_status");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");

            entity.HasOne(d => d.Device).WithMany(p => p.DeviceCredentials)
                .HasForeignKey(d => d.DeviceId)
                .HasConstraintName("device_credentials_device_id_fkey");
        });

        modelBuilder.Entity<DeviceHeartbeat>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_heartbeats_pkey");

            entity.ToTable("device_heartbeats");

            entity.HasIndex(e => new { e.DeviceId, e.ReceivedAt }, "idx_device_heartbeats_device_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.AgentVersion)
                .HasMaxLength(30)
                .HasColumnName("agent_version");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("received_at");
            entity.Property(e => e.SourceIp).HasColumnName("source_ip");

            entity.HasOne(d => d.Device).WithMany(p => p.DeviceHeartbeats)
                .HasForeignKey(d => d.DeviceId)
                .HasConstraintName("device_heartbeats_device_id_fkey");
        });

        modelBuilder.Entity<DeviceNetworkInterface>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_network_interfaces_pkey");

            entity.ToTable("device_network_interfaces");

            entity.HasIndex(e => e.DeviceId, "uq_device_primary_interface")
                .IsUnique()
                .HasFilter("is_primary");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.InterfaceName)
                .HasMaxLength(100)
                .HasColumnName("interface_name");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
            entity.Property(e => e.LastReportedAt).HasColumnName("last_reported_at");
            entity.Property(e => e.MacAddress).HasColumnName("mac_address");
            entity.Property(e => e.SubnetCidr).HasColumnName("subnet_cidr");
            entity.Property(e => e.WolEnabled).HasColumnName("wol_enabled");
            entity.Property(e => e.WolSupported).HasColumnName("wol_supported");

            entity.HasOne(d => d.Device).WithOne(p => p.DeviceNetworkInterface)
                .HasForeignKey<DeviceNetworkInterface>(d => d.DeviceId)
                .HasConstraintName("device_network_interfaces_device_id_fkey");
        });

        modelBuilder.Entity<DeviceStatus>(entity =>
        {
            entity.HasKey(e => e.DeviceId).HasName("device_status_pkey");

            entity.ToTable("device_status");

            entity.Property(e => e.DeviceId)
                .ValueGeneratedNever()
                .HasColumnName("device_id");
            entity.Property(e => e.LastIpAddress).HasColumnName("last_ip_address");
            entity.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");
            entity.Property(e => e.LastStatusChangeAt).HasColumnName("last_status_change_at");
            entity.Property(e => e.ReportedPowerState)
                .HasMaxLength(20)
                .HasColumnName("reported_power_state");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Device).WithOne(p => p.DeviceStatus)
                .HasForeignKey<DeviceStatus>(d => d.DeviceId)
                .HasConstraintName("device_status_device_id_fkey");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.Code, "permissions_code_key").IsUnique();

            entity.HasIndex(e => new { e.Module, e.Action }, "permissions_module_action_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .HasColumnName("code");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Module)
                .HasMaxLength(50)
                .HasColumnName("module");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Code, "roles_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsSystemRole).HasColumnName("is_system_role");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions");
                        j.HasIndex(new[] { "PermissionId" }, "idx_role_permissions_permission");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<Guid>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("security_events_pkey");

            entity.ToTable("security_events");

            entity.HasIndex(e => new { e.DeviceId, e.OccurredAt }, "idx_security_events_device_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(60)
                .HasColumnName("event_type");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("occurred_at");
            entity.Property(e => e.Outcome)
                .HasMaxLength(20)
                .HasColumnName("outcome");

            entity.HasOne(d => d.Device).WithMany(p => p.SecurityEvents)
                .HasForeignKey(d => d.DeviceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("security_events_device_id_fkey");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey).HasName("system_settings_pkey");

            entity.ToTable("system_settings");

            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue)
                .HasColumnType("jsonb")
                .HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("system_settings_updated_by_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasColumnName("username");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.ToTable("user_roles");

            entity.HasIndex(e => e.RoleId, "idx_user_roles_role");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("user_roles_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_roles_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
