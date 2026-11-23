@echo off
:: Gaming Cafe Client Agent - Administrator Launcher
:: Runs the PowerShell installer with Elevated Permissions

echo Checking administrator privileges...
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Administrator privileges confirmed.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else (
    echo Requesting Administrator elevation...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"\"%~dp0install.ps1\"\"' -Verb RunAs"
)
pause
