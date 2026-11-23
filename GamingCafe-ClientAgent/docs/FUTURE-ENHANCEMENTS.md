# Gaming Cafe Client Agent — Future Enhancements Roadmap

## Phase 2: Client Session & Kiosk Shell (Planned)

1. **Full-Screen Kiosk Lock Screen**:
   - DirectX / WPF non-dismissable lock screen preventing patron desktop access prior to session activation.
   - Integration with Windows GINA / Credential Provider to lock CAD (`Ctrl+Alt+Del`), Alt+Tab, and Windows Key.
2. **Session Timer HUD**:
   - Non-intrusive floating HUD displaying remaining prepaid gaming time, cafe notifications, and food & drink order menu.
3. **Session Termination & Clean Reboot**:
   - Auto-logoff patron, terminate running game processes, and optionally restore clean system state using Deep Freeze or Windows Unified Write Filter (UWF).

---

## Phase 3: Game Launching & License Management (Planned)

1. **Integrated Game Launcher**:
   - One-click launch for Steam, Epic Games, Riot Games, Ubisoft Connect, and local titles.
2. **Commercial Cafe License Pool Management**:
   - Shared cafe license distribution for popular titles without requiring individual patron game purchases.

---

## Phase 4: Peripherals & Hardware Health Telemetry (Planned)

1. **Hardware Telemetry Expansion**:
   - GPU temperature, VRAM usage, and fan speeds via NVML (NVIDIA) and ADL (AMD).
   - Peripheral connectivity detection (gaming mice, mechanical keyboards, headsets).
2. **Automated Maintenance Mode**:
   - Nightly game updates and driver patch distribution triggered via backend Hub broadcast.
