using System;
using System.Collections.Generic;

namespace GamingCafe.Api.Services;

public interface IDeviceMonitoringConsole
{
    void RenderStartupDashboard(
        string appName,
        string appVersion,
        string environment,
        IEnumerable<string> listeningUrls,
        string? apiBaseUrl,
        string? openApiUrl,
        string postgresHost,
        int postgresPort,
        string postgresDatabase,
        bool postgresConnected,
        string hubUrl,
        int heartbeatInterval,
        int heartbeatTimeout,
        string heartbeatLoggingMode,
        string monitoringServiceStatus);

    void LogWaitingForRegistrations(string endpoint = "POST /api/devices/register");

    void LogRegistrationRequest(
        string displayName,
        string? deviceCode,
        Guid? localDeviceId,
        string? clientIp,
        string? agentVersion,
        string? hardwareFingerprint);

    void LogRegistrationValidationResult(bool success, string? details = null);

    void LogRegistrationSuccess(
        Guid deviceId,
        string deviceCode,
        string displayName,
        string status,
        string approvalEndpoint,
        bool isExistingRecord = false);

    void LogRegistrationFailure(string reason, int statusCode);

    void LogApprovalRequest(Guid deviceId, Guid? adminId, string approvalRoute = "POST /api/devices/{id}/approve");

    void LogApprovalSuccess(
        Guid deviceId,
        string deviceCode,
        Guid? adminId,
        DateTime approvedAt,
        string credentialStatus,
        string approvalRoute = "POST /api/devices/{id}/approve",
        string authEndpoint = "POST /api/auth/device/dev-login");

    void LogRejection(Guid deviceId, string deviceCode, Guid? adminId, string reason);

    void LogAuthenticationAttempt(Guid deviceId, string? clientIp);

    void LogAuthenticationResult(Guid deviceId, bool success, string? reason = null, DateTime? expiresAt = null);

    void LogSignalRConnected(
        Guid deviceId,
        string? deviceCode,
        string connectionId,
        string? clientIp,
        bool isReconnect);

    void LogSignalRDisconnected(
        Guid deviceId,
        string? deviceCode,
        string connectionId,
        string reason);

    void LogHeartbeat(
        Guid deviceId,
        string? deviceCode,
        long sequenceNumber,
        string? agentVersion,
        string? clientIp,
        DateTime timestampUtc,
        bool isStatusTransition = false);

    void LogDeviceOffline(
        Guid deviceId,
        string? deviceCode,
        DateTime? lastSeenUtc,
        int timeoutSeconds);

    void LogDeviceReconnected(
        Guid deviceId,
        string? deviceCode,
        string? clientIp);
}
