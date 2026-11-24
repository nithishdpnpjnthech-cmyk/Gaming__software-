# Gaming Cafe Client Agent — Testing & Validation Guide

## 1. Test Suite Architecture

The unit test project (`GamingCafe.ClientAgent.Tests`) provides 100% in-process test coverage for all core services without external network or broker dependencies:

```
tests/GamingCafe.ClientAgent.Tests/
├── ConfigurationValidationTests.cs   # WSS enforcement, timeout rules, URL parsing (6 tests)
├── CredentialProtectionTests.cs      # DPAPI encryption roundtrip & secure file storage (2 tests)
├── DeviceIdentityTests.cs            # GUID generation & corrupted file quarantine (2 tests)
├── EnrollmentServiceTests.cs         # 8-state enrollment transitions & revocation (4 tests)
├── HealthServiceTests.cs             # CPU, memory, uptime, and thread telemetry (2 tests)
├── HeartbeatTests.cs                 # Sequence numbering & RTT latency computation (2 tests)
├── MessageHandlerTests.cs            # Sliding cache de-duplication & handler routing (2 tests)
├── SecurityValidationTests.cs        # WSS scheme validation & secret log masking (2 tests)
├── MockConfigurationService.cs       # In-memory configuration test double
└── Program.cs                        # In-process test runner avoiding vstest socket issues
```

---

## 2. Executing Tests

Execute the automated test script:
```powershell
.\scripts\test.ps1
```

Or via the .NET CLI:
```powershell
dotnet run --project tests\GamingCafe.ClientAgent.Tests
```

### Expected Output (24 Tests, 0 Failures)
```
==================================================
 Gaming Cafe Client Agent - In-Process Test Runner
==================================================

[Running Suite] ConfigurationValidationTests
  [PASS] DefaultConfiguration_ShouldBeValid
  [PASS] InsecureScheme_WithRequireWssTrue_ShouldFailValidation(ws://localhost:5001/hubs/agent)
  [PASS] InsecureScheme_WithRequireWssTrue_ShouldFailValidation(http://localhost:5000/hubs/agent)
  [PASS] InsecureScheme_WithRequireWssTrue_ShouldFailValidation(ftp://localhost/hubs)
  [PASS] SecureWssScheme_ShouldPassValidation(wss://localhost:5001/hubs/agent)
  [PASS] SecureWssScheme_ShouldPassValidation(https://backend.gamingcafe.local/hubs/agent)
  [PASS] InvalidHeartbeatIntervals_ShouldFailValidation
  [PASS] MaxDelayLessThanInitialDelay_ShouldFailValidation

[Running Suite] CredentialProtectionTests
  [PASS] Dpapi_RoundTripEncryption_PreservesPlaintext
  [PASS] SecureCredentialStore_SavesAndLoadsCredentialsSecurely

[Running Suite] DeviceIdentityTests
  [PASS] GetOrCreateIdentity_GeneratesPersistentGuid
  [PASS] CorruptedIdentityFile_IsQuarantined_AndNewGenerated

[Running Suite] EnrollmentServiceTests
  [PASS] InitializeState_WhenNoCredentials_SetsUnregistered
  [PASS] HandleApprovalMessage_WhenApproved_TransitionsToApproved
  [PASS] HandleAuthenticationMessage_SavesCredentials_TransitionsToAuthenticated
  [PASS] HandleRevocationMessage_ClearsCredentials_TransitionsToRevoked

[Running Suite] HealthServiceTests
  [PASS] GetSnapshot_ReturnsReasonableMetrics
  [PASS] RecordHeartbeat_TracksSentAndAckedCounts

[Running Suite] HeartbeatTests
  [PASS] SendHeartbeat_WhenConnected_IncrementsSequenceNumber
  [PASS] HandleAcknowledgment_CalculatesLatencyAndResetsMissedCount

[Running Suite] MessageHandlerTests
  [PASS] ProcessInboundMessage_DispatchesToRegisteredHandler
  [PASS] DuplicateMessageId_IsIgnoredByDeduplication

[Running Suite] SecurityValidationTests
  [PASS] ValidateHubUrl_RejectsInsecureSchemesWhenWssMandatory
  [PASS] SecurityMasker_MasksSensitiveValues

--------------------------------------------------
Test Execution Summary: Passed: 24, Failed: 0, Total: 24
--------------------------------------------------
```
