
using GamingCafe.Api.Hubs;
using GamingCafe.Api.Configuration;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using GamingCafe.Api.Data;
using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Identity;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. PostgreSQL Database Configuration
// ============================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL DefaultConnection is missing.");
}

NpgsqlConnectionStringBuilder databaseConfig;

try
{
    databaseConfig =
        new NpgsqlConnectionStringBuilder(connectionString);
}
catch (ArgumentException)
{
    throw new InvalidOperationException(
        "Invalid PostgreSQL DefaultConnection configuration.");
}

if (string.IsNullOrWhiteSpace(databaseConfig.Host) ||
    string.IsNullOrWhiteSpace(databaseConfig.Database))
{
    throw new InvalidOperationException(
        "PostgreSQL host and database name are required.");
}

// Prevent the regular backend from using the
// isolated integration test database.
if (string.Equals(
        databaseConfig.Database,
        "gaming_cafe_test_db",
        StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "The integration test database must not be used " +
        "by the regular GamingCafe backend.");
}

builder.Services.AddDbContext<GamingCafeDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ============================================
// 2. Password Hasher
// ============================================

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

// ============================================
// 3. Application Services
// ============================================

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DeviceAuthService>();
builder.Services.AddScoped<DevDeviceAuthService>();
builder.Services.AddScoped<DeviceCredentialService>();
builder.Services.AddScoped<DeviceStatusService>();
builder.Services.AddSingleton<IDeviceMonitoringConsole, DeviceMonitoringConsole>();
builder.Services.AddScoped<IHeartbeatService, HeartbeatService>();
builder.Services.AddHostedService<DeviceOfflineMonitorService>();

// ============================================
// 4. Heartbeat Configuration
// ============================================

builder.Services.AddOptions<HeartbeatOptions>()
    .Bind(builder.Configuration.GetSection("Heartbeat"))
    .Validate(
        o => o.IntervalSeconds > 0 &&
             o.TimeoutSeconds >= o.IntervalSeconds,
        "Invalid heartbeat interval or timeout configuration."
    )
    .ValidateOnStart();

// ============================================
// 5. SignalR Configuration
// ============================================

builder.Services.AddSignalR();

// ============================================
// 6. JWT Configuration
// ============================================

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is missing.");

var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "JWT secret key is missing.");

byte[] jwtKey;

try
{
    jwtKey = Convert.FromBase64String(jwtSecret);
}
catch (FormatException)
{
    throw new InvalidOperationException(
        "JWT secret key must be a valid Base64 value.");
}

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT secret key must contain at least 32 bytes.");
}

// ============================================
// 7. JWT Authentication
// ============================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKey),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

        // Support SignalR WebSocket authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var path =
                    context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/agent"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// ============================================
// 8. Authorization Policies
// ============================================

builder.Services.AddAuthorization(options =>
{
    // Agent PC heartbeat authorization
    options.AddPolicy("agent.heartbeat", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("token_type", "device")
              .RequireClaim("device_id"));

    // Admin device permissions
    options.AddPolicy("devices.view", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("permission", "devices.view"));

    options.AddPolicy("devices.approve", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("permission", "devices.approve"));

    options.AddPolicy("devices.reject", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("permission", "devices.reject"));

    options.AddPolicy("devices.send_command", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("permission", "devices.send_command"));
});

// ============================================
// 9. Controllers and OpenAPI
// ============================================

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================
// 10. PostgreSQL Startup Verification & Summary Banner
// ============================================

{
    var appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    var hubUrl = "/hubs/agent";
    var heartbeatSection = builder.Configuration.GetSection("Heartbeat");
    var interval = heartbeatSection.GetValue<int>("IntervalSeconds", 15);
    var timeout = heartbeatSection.GetValue<int>("TimeoutSeconds", 45);

    app.Logger.LogInformation("================================================================================");
    app.Logger.LogInformation("  GAMING CAFE MANAGEMENT SYSTEM - BACKEND API (.NET 10)");
    app.Logger.LogInformation("  Version:      {Version}", appVersion);
    app.Logger.LogInformation("  Environment:  {Environment}", app.Environment.EnvironmentName);
    app.Logger.LogInformation("  PostgreSQL:   Host={Host}, Port={Port}, Database={Database}", 
        databaseConfig.Host, databaseConfig.Port, databaseConfig.Database);
    app.Logger.LogInformation("  SignalR Hub:  {HubUrl} (Auth: agent.heartbeat)", hubUrl);
    app.Logger.LogInformation("  Heartbeat:    Interval={Interval}s, Timeout={Timeout}s", interval, timeout);
    app.Logger.LogInformation("  Services:     DeviceOfflineMonitorService (active)");
    app.Logger.LogInformation("================================================================================");

    try
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<GamingCafeDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try
        {
            var connection =
                dbContext.Database.GetDbConnection();

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                "SELECT current_database();";

            var actualDatabase =
                Convert.ToString(
                    await command.ExecuteScalarAsync());

            if (!string.Equals(
                    actualDatabase,
                    databaseConfig.Database,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Connected PostgreSQL database does not " +
                    "match the configured database.");
            }

            app.Logger.LogInformation(
                "PostgreSQL Connection: SUCCESS (Database: {Database})",
                actualDatabase);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "PostgreSQL startup verification failed. " +
            "Check the database configuration and server availability.");

        throw;
    }
}

// ============================================
// 11. One-time Super Admin Bootstrap
// ============================================

if (args.Contains("--bootstrap-super-admin"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Super Admin bootstrap is allowed only in Development.");
    }

    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<GamingCafeDbContext>();

    var passwordHasher = scope.ServiceProvider
        .GetRequiredService<IPasswordHasher<User>>();

    await RbacSeeder.SeedAsync(dbContext);

    await SuperAdminBootstrapper.RunAsync(
        dbContext,
        passwordHasher);

    return;
}

// ============================================
// 12. Development RBAC Seeder
// ============================================

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<GamingCafeDbContext>();

    await RbacSeeder.SeedAsync(dbContext);
}

// ============================================
// 13. HTTP Request Pipeline
// ============================================

app.UseMiddleware<GamingCafe.Api.Middleware.RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Allow temporary local HTTP testing in Development.
// Configure trusted HTTPS for actual Agent PC deployment.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Authentication must execute before authorization
app.UseAuthentication();
app.UseAuthorization();

// ============================================
// 14. Controller Endpoints
// ============================================

app.MapControllers();

// ============================================
// 15. Secure SignalR Agent Hub
// ============================================

app.MapHub<AgentHub>("/hubs/agent", options =>
{
    options.CloseOnAuthenticationExpiration = true;
})
.RequireAuthorization("agent.heartbeat");

// ============================================
// 16. Live Monitoring Dashboard on Application Started
// ============================================

app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var addressFeature = server?.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
    var addresses = addressFeature?.Addresses?.ToList() ?? new List<string>();

    if (addresses.Count == 0)
    {
        var configuredUrl = builder.Configuration["Kestrel:Endpoints:Http:Url"];
        if (!string.IsNullOrEmpty(configuredUrl))
        {
            addresses.Add(configuredUrl);
        }
    }

    var monitoringConsole = app.Services.GetRequiredService<IDeviceMonitoringConsole>();
    var appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    var heartbeatSection = builder.Configuration.GetSection("Heartbeat");
    var interval = heartbeatSection.GetValue<int>("IntervalSeconds", 15);
    var timeout = heartbeatSection.GetValue<int>("TimeoutSeconds", 45);
    var mode = heartbeatSection.GetValue<string>("LoggingMode", "Normal") ?? "Normal";

    var primaryUrl = addresses.FirstOrDefault() ?? "http://0.0.0.0:5022";
    var openApiUrl = app.Environment.IsDevelopment() ? $"{primaryUrl.TrimEnd('/')}/openapi/v1.json" : null;

    monitoringConsole.RenderStartupDashboard(
        appName: "Gaming Cafe Management System",
        appVersion: appVersion,
        environment: app.Environment.EnvironmentName,
        listeningUrls: addresses,
        apiBaseUrl: primaryUrl,
        openApiUrl: openApiUrl,
        postgresHost: databaseConfig.Host ?? "localhost",
        postgresPort: databaseConfig.Port > 0 ? databaseConfig.Port : 5432,
        postgresDatabase: databaseConfig.Database ?? "gamingcafe1",
        postgresConnected: true,
        hubUrl: "/hubs/agent",
        heartbeatInterval: interval,
        heartbeatTimeout: timeout,
        heartbeatLoggingMode: mode,
        monitoringServiceStatus: "Active (DeviceOfflineMonitorService)");
});

// ============================================
// 17. Start Kestrel Server
// ============================================

app.Run();