
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using GamingCafe.Api.Data;
using GamingCafe.Api.Services;
using GamingCafe.Api.Hubs;
using Microsoft.AspNetCore.Identity;
using GamingCafe.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Register PostgreSQL database
builder.Services.AddDbContext<GamingCafeDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Register password hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Register application services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();

// Read JWT settings from User Secrets / configuration
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is missing.");

var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key is missing.");

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

// Register JWT authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/agent"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Register permission-based authorization policies
builder.Services.AddAuthorization(options =>
{
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

// Register CORS for frontend integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register controllers and SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

var app = builder.Build();

// One-time Super Admin bootstrap mode
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

// Run RBAC seeder only in Development
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<GamingCafeDbContext>();

        await RbacSeeder.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("PostgreSQL database connection failed on startup: {Message}. The API is running, but database-dependent endpoints require PostgreSQL to be started with the schema applied.", ex.Message);
    }
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors();

// Authentication must run before authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHub>("/hubs/agent");

app.Run();
