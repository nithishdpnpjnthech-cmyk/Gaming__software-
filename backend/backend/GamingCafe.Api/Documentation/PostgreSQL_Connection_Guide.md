# PostgreSQL Connection Guide

## 1. Connection String Specification
The backend connects to PostgreSQL using Npgsql:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=gamingcafe1;Username=postgres;Password=root"
}
```

## 2. Configuration Options & Defaults
- **Host**: `localhost` (or LAN IP/hostname if running on a dedicated database server)
- **Port**: `5432` (standard PostgreSQL service port)
- **Database**: `gamingcafe1` (target schema matching the authoritative ER diagram)
- **Username**: `postgres`
- **Password**: configured password (`root` for local environment)

## 3. Verifying Connection & Health Check
Execute via PowerShell:
```powershell
$env:PGPASSWORD="root"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -p 5432 -d gamingcafe1 -c "
SELECT 
    current_database() AS database, 
    current_user AS user, 
    inet_server_addr() AS server_ip, 
    inet_server_port() AS server_port, 
    version() AS pg_version;
"
```

## 4. Connection Pool & Performance Tuning
- Connection pooling is handled automatically by Npgsql.
- In `Program.cs`, connection string validation is performed at startup, aborting execution with actionable diagnostics if `Host` or `Database` is missing.
- Row-level locking (`SELECT ... FOR UPDATE`) is used in `HeartbeatService.RecordAsync` and `DevicesController.ApproveDevice` to guarantee transactional serialization under concurrent load.
