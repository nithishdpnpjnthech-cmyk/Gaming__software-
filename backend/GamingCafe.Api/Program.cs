
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
builder.Services.AddScoped<IHeartbeatService, HeartbeatService>();

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
// 10. PostgreSQL Startup Verification
// ============================================

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation(
        "========== PostgreSQL Configuration ==========");

    app.Logger.LogInformation(
        "PostgreSQL Host: {Host}",
        databaseConfig.Host);

    app.Logger.LogInformation(
        "PostgreSQL Port: {Port}",
        databaseConfig.Port);

    app.Logger.LogInformation(
        "Configured Database: {Database}",
        databaseConfig.Database);

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
                "Connected Database: {Database}",
                actualDatabase);

            app.Logger.LogInformation(
                "PostgreSQL Connection: SUCCESS");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
    catch
    {
        app.Logger.LogCritical(
            "PostgreSQL startup verification failed. " +
            "Check the database configuration and server availability.");

        throw;
    }

    app.Logger.LogInformation(
        "==============================================");
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
// 16. Start Kestrel Server
// ============================================

app.Run();