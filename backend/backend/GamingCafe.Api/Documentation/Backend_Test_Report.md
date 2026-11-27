# Backend Test Report

## Test Execution Results

### 1. Backend Test Suite
Executed command:
```powershell
dotnet test backend/GamingCafe.Api.Tests/GamingCafe.Api.Tests.csproj
```

**Results**:
- **Total Tests**: 53
- **Passed**: 53
- **Failed**: 0
- **Skipped**: 0
- **Duration**: ~2.1 seconds

**Coverage**:
- Unit tests: JWT creation, password hashing, DTO validation, options validation (34 passed).
- PostgreSQL integration tests: `HeartbeatServiceIntegrationTests`, `DeviceStatusIntegrationTests`, row locking, and rollback on unapproved devices (19 passed against PostgreSQL).

### 2. Client Agent Compatibility Tests
Executed command:
```powershell
dotnet test GamingCafe-ClientAgent/tests/GamingCafe.ClientAgent.Tests/GamingCafe.ClientAgent.Tests.csproj
```

**Results**:
- **Total Tests**: 99
- **Passed**: 99
- **Failed**: 0
- **Skipped**: 0
- **Duration**: ~14 seconds
