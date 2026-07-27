using System.Text.Json;
using System.Threading.RateLimiting;
using LivingRealms.Api.Features;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, services, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "LivingRealms.Api")
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddProblemDetails();
builder.Services.AddLivingRealmsInfrastructure(builder.Configuration);
builder.Services.Configure<WorldSimulationOptions>(builder.Configuration.GetSection(WorldSimulationOptions.SectionName));
builder.Services
    .AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("gameplay", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("world-control", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                // Play-test controls must remain usable even when combat has
                // exhausted the much busier gameplay request bucket.
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var databaseConnection = builder.Configuration.GetConnectionString("GameDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:GameDatabase is required.");

builder.Services.AddHealthChecks()
    .AddNpgSql(databaseConnection, name: "postgresql", tags: ["ready"]);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        try
        {
            await using var populationScope = app.Services.CreateAsyncScope();
            var population = populationScope.ServiceProvider.GetRequiredService<WorldPopulationService>();
            await population.EnsureStonehavenResidentsAsync();
            await population.EnsureDarkwoodClanMembersAsync();
            Program.LogWorldPopulationMaterialized(app.Logger);
        }
        catch (Exception exception)
        {
            Program.LogWorldPopulationMaterializationFailed(app.Logger, exception);
        }
    }));
}

app.UseExceptionHandler();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api", () => Results.Ok(new
{
    service = "Living Realms Game API",
    phase = 8,
    status = "settlement-development-ready",
    serverTimeCentral = CentralClock.Now
}));

app.MapPhaseTwoEndpoints();
app.MapPhaseFourEndpoints();
app.MapPhaseFiveEndpoints();
app.MapPhaseSixEndpoints();
app.MapPhaseSevenEndpoints();
app.MapPhaseSevenBRaidEndpoints();
app.MapDevelopmentEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

app.Run();

static Task WriteHealthResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        checkedAtUtc = DateTimeOffset.UtcNow,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            durationMs = entry.Value.Duration.TotalMilliseconds,
            description = entry.Value.Description
        })
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

public partial class Program
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Reported world populations were materialized as persistent actors.")]
    public static partial void LogWorldPopulationMaterialized(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Error,
        Message = "World population materialization failed after API startup.")]
    public static partial void LogWorldPopulationMaterializationFailed(
        Microsoft.Extensions.Logging.ILogger logger,
        Exception exception);
}
