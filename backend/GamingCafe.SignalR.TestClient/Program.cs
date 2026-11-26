using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

var baseUrl = Environment.GetEnvironmentVariable(
    "GAMINGCAFE_TEST_SERVER_URL")
    ?? "http://localhost:5022";

var deviceIdText = Environment.GetEnvironmentVariable(
    "GAMINGCAFE_TEST_DEVICE_ID");

var enrollmentSecret = Environment.GetEnvironmentVariable(
    "GAMINGCAFE_TEST_DEVICE_SECRET");

if (!Guid.TryParse(deviceIdText, out var deviceId) ||
    deviceId == Guid.Empty ||
    string.IsNullOrWhiteSpace(enrollmentSecret))
{
    Console.WriteLine("Device configuration is missing.");
    return;
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(15)
};

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};


string? cachedDeviceToken = null;

DateTimeOffset cachedTokenExpiry = DateTimeOffset.MinValue;

using var tokenLock = new SemaphoreSlim(1, 1);

async Task<string?> GetDeviceTokenAsync()
{
    await tokenLock.WaitAsync(cancellation.Token);

    try
    {
        // Reuse the existing JWT until close to expiry.
        if (!string.IsNullOrWhiteSpace(cachedDeviceToken) &&
            cachedTokenExpiry >
                DateTimeOffset.UtcNow.AddSeconds(60))
        {
            Console.WriteLine("[AUTH] Reusing cached JWT.");

            return cachedDeviceToken;
        }

        using var response = await httpClient.PostAsJsonAsync(
            "/api/auth/device/dev-login",
            new
            {
                deviceId,
                enrollmentSecret
            },
            cancellation.Token);

        response.EnsureSuccessStatusCode();

        var login = await response.Content
            .ReadFromJsonAsync<DeviceLoginResponse>(
                cancellationToken: cancellation.Token);

        if (login is null ||
            login.DeviceId != deviceId ||
            string.IsNullOrWhiteSpace(login.AccessToken))
        {
            throw new InvalidOperationException(
                "Invalid device authentication response.");
        }

        cachedDeviceToken = login.AccessToken;
        cachedTokenExpiry = login.ExpiresAt;

        Console.WriteLine(
            $"[AUTH] New JWT received. Expires: {cachedTokenExpiry:O}");

        return cachedDeviceToken;
    }
    finally
    {
        tokenLock.Release();
    }
}


await using var connection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/agent", options =>
    {
        options.Transports = HttpTransportType.WebSockets;
        options.AccessTokenProvider = GetDeviceTokenAsync;
    })
    .WithAutomaticReconnect(
        new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        })
    .Build();

connection.ServerTimeout = TimeSpan.FromSeconds(30);
connection.KeepAliveInterval = TimeSpan.FromSeconds(10);

connection.On<HeartbeatAck>(
    "HeartbeatAck",
    ack =>
    {
        Console.WriteLine(
            $"[ACK] Sequence: {ack.SequenceNumber}");

        Console.WriteLine(
            $"      Device: {ack.DeviceId}");

        Console.WriteLine(
            $"      Original message: {ack.OriginalMessageId}");

        Console.WriteLine(
            $"      Server UTC: {ack.ServerTimestampUtc:O}");
    });

connection.Reconnecting += error =>
{
    Console.WriteLine("[SIGNALR] Reconnecting...");
    return Task.CompletedTask;
};

connection.Reconnected += connectionId =>
{
    Console.WriteLine("[SIGNALR] Reconnected.");
    return Task.CompletedTask;
};

connection.Closed += error =>
{
    Console.WriteLine("[SIGNALR] Connection closed.");
    cancellation.Cancel();
    return Task.CompletedTask;
};

try
{
    Console.WriteLine("=== GamingCafe SignalR Test Client ===");
    Console.WriteLine($"Server: {baseUrl}");
    Console.WriteLine($"Device: {deviceId}");

    await connection.StartAsync(cancellation.Token);

    Console.WriteLine(
        $"[SIGNALR] State: {connection.State}");

    Console.WriteLine(
        $"[SIGNALR] Connection ID: {connection.ConnectionId}");

    Console.WriteLine("[SIGNALR] WebSocket connected.");

    long sequenceNumber = 0;

    while (!cancellation.IsCancellationRequested)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            var message = new
            {
                messageId = Guid.NewGuid().ToString("N"),
                version = "1.0",
                messageType = "Heartbeat",
                timestampUtc = DateTimeOffset.UtcNow,
                deviceId,
                sequenceNumber = ++sequenceNumber,
                agentVersion = "1.0.0",
                upTimeSeconds = 120.0,
                cpuPercent = 25.5,
                memoryUsageMb = 512.0
            };

            try
            {
                await connection.InvokeAsync(
                    "SendHeartbeat",
                    message,
                    cancellation.Token);

                Console.WriteLine(
                    $"[SENT] Heartbeat #{sequenceNumber}");
            }
            catch (Exception ex)
                when (!cancellation.IsCancellationRequested)
            {
                Console.WriteLine(
                    $"[ERROR] Heartbeat failed: {ex.Message}");
            }
        }

        await Task.Delay(
            TimeSpan.FromSeconds(5),
            cancellation.Token);
    }
}
catch (OperationCanceledException)
    when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("[STOP] Test cancelled.");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
}
finally
{
    await connection.StopAsync();

    Console.WriteLine("[STOP] Test client stopped.");
}

public sealed record DeviceLoginResponse(
    Guid DeviceId,
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record HeartbeatAck(
    string MessageId,
    string Version,
    string MessageType,
    Guid DeviceId,
    DateTimeOffset TimestampUtc,
    long SequenceNumber,
    string OriginalMessageId,
    DateTimeOffset ServerTimestampUtc);