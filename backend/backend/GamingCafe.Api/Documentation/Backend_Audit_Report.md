# Backend Audit Report

## 1. Executive Summary
An exhaustive audit was conducted on the Gaming Cafe Management System backend (`GamingCafe.Api`). The backend was evaluated across .NET 10 compilation, configuration, directory portability, PostgreSQL 18 relational schema, Entity Framework Core mappings, SignalR real-time connectivity, device authentication, and background services.

## 2. Environment & Project Properties
- **Framework**: .NET 10 (`net10.0`)
- **SDK Project Type**: `Microsoft.NET.Sdk.Web`
- **Current Repository Location**: `C:\Users\Roope\OneDrive\Desktop\Gamingcafe1\Gaming__software-\backend\GamingCafe.Api`
- **Database Engine**: PostgreSQL 18.x running on port `5432`
- **Default Database**: `gamingcafe1` (owner `postgres`, password `root`)

## 3. Stale Path & Build Artifacts Audit
- **Problem**: When projects are copied or cloned from other environments, MSBuild caches in `obj/Debug/net10.0/` retain absolute paths (e.g., `C:\Users\Personal\Desktop\...`). When ASP.NET Core initializes static web assets manifests (`rjsmrazor.dswa.cache.json`, `staticwebassets.build.json`), it throws `System.IO.DirectoryNotFoundException: ... obj\Debug\net10.0\jsmodules`.
- **Resolution**: Cleaned `bin/` and `obj/` trees, executed a clean `dotnet restore`, and recompiled with `dotnet build --no-incremental`. The project now builds cleanly from its current location with 0 errors.

## 4. Entity Framework Core & PostgreSQL Alignment
- **DbContext**: `GamingCafeDbContext` configured with Npgsql provider (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`).
- **Extensions**: `pgcrypto` extension enabled for cryptographic UUID generation.
- **Relational Alignment**: All 17 tables from the authoritative ER diagram are mapped in Fluent API and present in PostgreSQL.
