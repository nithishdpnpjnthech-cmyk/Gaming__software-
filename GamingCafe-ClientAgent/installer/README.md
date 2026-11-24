# Gaming Cafe Client Agent — Installer & Deployment Package

This directory provides guidelines and deployment artifacts for provisioning the Gaming Cafe Client Agent across fleet gaming machines in a commercial gaming cafe.

## Target Machine Prerequisites
- Windows 10 / Windows 11 (64-bit)
- Network connectivity to Admin PC on cafe LAN
- Administrator elevation during initial installation (not required at runtime)

## Production Deployment Flow
1. **Publish Release Binaries**:
   ```powershell
   dotnet publish src/GamingCafe.ClientAgent/GamingCafe.ClientAgent.csproj -c Release -o publish/
   ```
2. **Copy Binaries**:
   Transfer `publish/` contents to `C:\Program Files\GamingCafe\ClientAgent\` on the target PC.
3. **Configure Settings**:
   Edit `appsettings.json` in the binary directory to point `AdminApiBaseUrl` to the cafe Admin PC (e.g. `https://192.168.1.100:5001`).
4. **Run Installation Script**:
   Open PowerShell as Administrator and execute:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Install-ClientAgent.ps1 -BinaryPath "C:\Program Files\GamingCafe\ClientAgent\GamingCafe.ClientAgent.exe"
   ```
5. **Verify Installation**:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Get-ClientAgentStatus.ps1
   ```
6. **Start Service**:
   ```powershell
   Start-Service GamingCafeClientAgent
   ```
