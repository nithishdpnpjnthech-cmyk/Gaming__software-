# Gaming Cafe Client Agent – Complete Codebase Walkthrough & Technical Curriculum

Welcome to the comprehensive technical documentation and learning curriculum for the **Gaming Cafe Management System Client Agent** (`GamingCafe.ClientAgent`).

This documentation suite is created for developers, architects, and engineering leads who want to master the entire codebase from beginner to senior architect level.

---

## Master Document Index

| # | Document | Focus & Key Concepts |
| :-: | :--- | :--- |
| **01** | [**Project Architecture & System Overview**](01-Project-Architecture.md) | High-level role, system context, directory structure, module responsibility map, layered architecture, and quality attributes. |
| **02** | [**Startup & Dependency Injection**](02-Startup-and-Dependency-Injection.md) | Generic Host lifecycle, `Program.cs` anatomy, resolving DI circular dependencies, service lifetimes (`Singleton`/`Transient`/`Scoped`), and startup sequence. |
| **03** | [**C# Code Line-by-Line Deep Dive**](03-CSharp-Code-Line-by-Line.md) | Detailed line-by-line breakdown of core files: `ClientAgentWorker.cs`, `ConfigurationValidator.cs`, `MessageHandler.cs`, `ConnectionStateManager.cs`, and `SecurityMasker.cs`. |
| **04** | [**Device Identity & Security Subsystem**](04-Device-Identity-and-Security.md) | `PC-XXXXXXXX` hardware identity generation, decoupling `DeviceId` from `BackendDeviceId`, Windows DPAPI encryption, and corruption quarantine. |
| **05** | [**Backend API Integration & Device Enrollment**](05-Backend-API-and-Enrollment.md) | REST client architecture, `POST /api/devices/register` contract, handling 201 Created vs 409 Conflict, and enrollment state transitions. |
| **06** | [**SignalR WebSocket & Heartbeat Subsystem**](06-SignalR-WebSocket-and-Heartbeat.md) | WSS transport, JWT access token provider, monotonic sequence heartbeats, latency calculation, full jitter exponential backoff, and dormant status explanation. |
| **07** | [**Logging, Error Handling & Testing**](07-Logging-Error-Handling-and-Testing.md) | Asynchronous daily rolling file logger, OWASP log masking, graceful `Ctrl+C` shutdown, and test strategy across all 56 passing tests. |
| **08** | [**Complete Runtime Execution Flow**](08-Complete-Runtime-Flow.md) | 10-phase execution lifecycle, path probing, validation, identity loading, registration, maintenance loop, and complete Mermaid sequence diagram. |
| **09** | [**Code Review, Architecture Audit & Technical Gaps**](09-Code-Review-and-Technical-Gaps.md) | Senior architect review: production-ready features, dormant features awaiting backend support, backend gaps, and future roadmap. |
| **10** | [**Learning Curriculum & Technical Interview Questions**](10-Learning-Plan-and-Interview-Questions.md) | 15 learning modules with code takeaways and 75 in-depth technical interview defense questions with senior-architect-level answers. |

---

## Recommended Study Pathway

To gain complete mastery of the codebase, follow this sequential 4-stage learning path:

### Stage 1: Foundational Architecture (Days 1–2)
1. Read **[01-Project-Architecture.md](01-Project-Architecture.md)** to grasp the high-level role of the Client Agent in a gaming center.
2. Read **[02-Startup-and-Dependency-Injection.md](02-Startup-and-Dependency-Injection.md)** to understand how .NET 10 hosts applications and how circular dependencies were permanently solved.

### Stage 2: Core Domain Logic & Security (Days 3–4)
3. Study **[03-CSharp-Code-Line-by-Line.md](03-CSharp-Code-Line-by-Line.md)** to inspect the actual implementation patterns in C#.
4. Read **[04-Device-Identity-and-Security.md](04-Device-Identity-and-Security.md)** to master Windows DPAPI encryption and local hardware identity persistence.

### Stage 3: Communication & Telemetry (Days 5–6)
5. Read **[05-Backend-API-and-Enrollment.md](05-Backend-API-and-Enrollment.md)** to understand REST registration contracts and why 409 Conflict is never treated as approval.
6. Study **[06-SignalR-WebSocket-and-Heartbeat.md](06-SignalR-WebSocket-and-Heartbeat.md)** to learn WebSocket real-time communication, monotonic ping/pong, and jittered exponential backoff.
7. Read **[07-Logging-Error-Handling-and-Testing.md](07-Logging-Error-Handling-and-Testing.md)** to inspect rolling file logging and the 56-test verification suite.

### Stage 4: Orchestration, Review & Technical Defense (Day 7+)
8. Review **[08-Complete-Runtime-Flow.md](08-Complete-Runtime-Flow.md)** to trace the entire end-to-end lifecycle diagram.
9. Inspect **[09-Code-Review-and-Technical-Gaps.md](09-Code-Review-and-Technical-Gaps.md)** to understand backend-blocked features and next steps.
10. Test your knowledge using **[10-Learning-Plan-and-Interview-Questions.md](10-Learning-Plan-and-Interview-Questions.md)** by answering all 75 architectural interview questions.

---

## Project Verification Status
- **Target Framework**: .NET 10 (`net10.0`)
- **Unit / Integration Tests**: 56 Total Tests across 14 test classes
- **Pass Rate**: 100% (56 Passed, 0 Failed)
- **Zero Modifications to Backend**: The `backend/` directory remains 100% untouched and unchanged.
