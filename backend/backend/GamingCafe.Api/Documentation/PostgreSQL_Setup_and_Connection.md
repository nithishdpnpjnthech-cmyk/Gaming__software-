# PostgreSQL Setup and Connection Guide

## 1. Connection Configuration
The backend is configured to connect to PostgreSQL 18 via Npgsql:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=gamingcafe1;Username=postgres;Password=root"
}
```

## 2. Database Verification
To verify database connectivity from PowerShell:
```powershell
$env:PGPASSWORD="root"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -p 5432 -d gamingcafe1 -c "SELECT current_database(), current_user, version();"
```

## 3. Database Creation (if setting up fresh)
```powershell
$env:PGPASSWORD="root"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -p 5432 -c "CREATE DATABASE gamingcafe1;"
```

## 4. Connection Resiliency
- Connection pool size defaults to standard Npgsql pooling.
- Transactions are scoped via `await using var tx = await context.Database.BeginTransactionAsync()`.
- Row-level locking (`SELECT ... FOR UPDATE`) is enforced in `HeartbeatService` and approval actions to avoid race conditions.
