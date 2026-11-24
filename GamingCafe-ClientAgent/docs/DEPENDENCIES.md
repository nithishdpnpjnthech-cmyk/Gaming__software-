# Gaming Cafe Client Agent — Dependencies & Compatibility

## 1. Framework & Runtime Dependencies

| Component | Target Version | Source / Pack | Purpose |
|---|---|---|---|
| **.NET SDK** | `10.0.404` | C:\Users\Roope\AppData\Local\Microsoft\dotnet | Compiler, build system, and CLI tools. |
| **Target Framework** | `net10.0` (with Windows platform targeting) | Microsoft.NETCore.App | Core runtime and BCL libraries. |
| **Windows Desktop Pack** | `10.0.15` | Microsoft.WindowsDesktop.App | Access to Windows DPAPI and system service controllers. |

---

## 2. NuGet Package Manifest

All packages are pinned to stable, compatible versions stored locally in `C:\Users\Roope\.nuget\packages` and configured via `nuget.config`:

### Core Service Project (`GamingCafe.ClientAgent.csproj`)
- `Microsoft.AspNetCore.SignalR.Client` (Version `11.0.0`): Provides high-performance WebSocket client connection, binary framing, and automatic reconnection pipeline.
- `Microsoft.Extensions.Hosting.WindowsServices` (Version `10.0.15`): Integrates .NET generic host with Windows Service Control Manager (`Services.msc`).
- `System.Security.Cryptography.ProtectedData` (Version `11.0.0`): Windows DPAPI wrappers for machine-level hardware encryption.

### Unit Test Project (`GamingCafe.ClientAgent.Tests.csproj`)
- `Microsoft.NET.Test.Sdk` (Version `17.14.1`): MSBuild test targets.
- `xunit` (Version `2.9.3`): Unit testing engine.
- `xunit.runner.visualstudio` (Version `3.1.4`): Test runner integration.
- `coverlet.collector` (Version `6.0.4`): Code coverage collection.

---

## 3. Unnecessary Dependency Policy

The Client Agent strictly avoids heavyweight third-party libraries:
- **No external JSON parsers**: Uses native `System.Text.Json` (zero Newtonsoft.Json dependency).
- **No external HTTP/REST clients**: Uses native SignalR client without RestSharp or Flurl.
- **No third-party logging sinks**: Features a lightweight zero-allocation rotating file logger (`FileLoggerProvider`) without Serilog or NLog overhead.
- **No third-party IoC containers**: Uses native `Microsoft.Extensions.DependencyInjection`.
