# Gaming Cafe Client Agent — Project Overview

## 1. Executive Summary

The **Gaming Cafe Client Agent** (Phase 1) is a specialized, production-ready Windows background service developed for high-performance Gaming PCs in a gaming cafe or esports arena. The agent is built with **C# and .NET 10** and runs autonomously under the `NT AUTHORITY\SYSTEM` service account without requiring an interactive Windows user session.

Its core purpose is to maintain a persistent, secure, real-time communication channel with the central Gaming Cafe administration backend, providing:
- Unique cryptographic device identity and environmental fingerprinting.
- Out-of-band device enrollment, registration, and administrative approval workflow.
- High-frequency heartbeat telemetry with latency measurement and health reporting.
- Automatic network disruption recovery and resilient reconnection with exponential backoff and randomized cryptographic jitter.
- Local credential encryption at rest using Windows Data Protection API (DPAPI) with LocalMachine scope.
- Zero inbound attack surface: strictly outbound client with no listening ports.

---

## 2. Core Operational Scope (Phase 1)

Phase 1 focuses exclusively on the robust network, security, identity, and lifecycle foundation:

| Feature Area | In-Scope (Phase 1) | Out-of-Scope (Future Phases) |
|---|---|---|
| **Networking** | SignalR over WebSocket Secure (WSS) exclusively. | REST API polling, Long Polling, SSE. |
| **Authentication** | Dynamic JWT token lifecycle & DPAPI encryption. | User login UI, member portal. |
| **Enrollment** | 8-stage state machine over SignalR protocol. | Multi-tenant cloud federation. |
| **Telemetry** | CPU %, Memory MB, process threads, heartbeat RTT latency. | Session timers, billing calculations. |
| **System Control** | Windows Service lifecycle, auto-restart on failure. | Remote shell, PowerShell execution, screen capture. |
| **User Experience** | Silent background execution with zero desktop interference. | Game launchers, desktop lock screen, kiosk shell. |

---

## 3. Technology Stack Summary

- **Framework**: .NET 10.0 (net10.0-windows / net10.0, win-x64)
- **Language**: C# 14
- **Hosting**: Microsoft.Extensions.Hosting.WindowsServices 10.0.15
- **Communication**: Microsoft.AspNetCore.SignalR.Client 11.0.0
- **Cryptography**: System.Security.Cryptography.ProtectedData 11.0.0 (Windows DPAPI)
- **Serialization**: System.Text.Json 11.0.0
- **Testing**: xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1
