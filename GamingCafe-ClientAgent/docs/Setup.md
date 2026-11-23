# Gaming Cafe Client Agent — Development & Environment Setup

## Prerequisites
- **Operating System**: Windows 10 / 11 (64-bit)
- **.NET SDK**: .NET 10 SDK (Version 10.0.404 or later)
- **Development Environment**: Visual Studio Code
- **Shell**: PowerShell 7+ or Windows PowerShell 5.1

---

## Workspace Setup in VS Code

1. Open VS Code in the project root:
   ```powershell
   code "C:\Users\Roope\OneDrive\Desktop\Gamingcafe1\Gaming__software-\GamingCafe-ClientAgent"
   ```

2. Recommended VS Code Extensions:
   - **C# Dev Kit** (`ms-dotnettools.csdevkit`)
   - **C# Extension** (`ms-dotnettools.csharp`)
   - **PowerShell** (`ms-vscode.powershell`)

---

## Building the Solution

Execute the build script from PowerShell:

```powershell
# Restore and build in Release configuration
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release

# Or via dotnet CLI
dotnet restore
dotnet build -c Release
```

---

## Running in Development Mode

The Client Agent can be executed directly as a console application for rapid debugging without installing the Windows Service:

```powershell
# Set environment to Development (enables verbose debug logs and local data directory)
$env:DOTNET_ENVIRONMENT = "Development"

# Run the agent console
dotnet run --project src/GamingCafe.ClientAgent/GamingCafe.ClientAgent.csproj
```

When running in Development mode:
- Configuration is loaded from `appsettings.Development.json`.
- State and logs are written to `./dev_data` relative to the project directory.
- `Ctrl+C` initiates a graceful shutdown.

---

## Running the Unit Test Suite

Execute the test script from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1

# Or via dotnet CLI
dotnet run --project tests/GamingCafe.ClientAgent.Tests/GamingCafe.ClientAgent.Tests.csproj
```

All 34 unit tests validate:
- Cryptographic Device ID generation & persistence
- Resilient recovery from corrupt storage files
- Strict configuration, HTTPS, and WSS validation rules
- Multi-step enrollment state transitions
- Rejection and revocation credential purging
- SignalR reconnect policy exponential backoff and jitter bounds
- Heartbeat dual-transport (SignalR primary with REST fallback)
- Network failure, timeout, and malformed response resilience
