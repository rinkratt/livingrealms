using LivingRealms.Infrastructure;
using LivingRealms.Infrastructure.Simulation;
using LivingRealms.Worker;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "LivingRealms.Worker")
    .WriteTo.Console(new JsonFormatter()));

builder.Services.Configure<WorldWorkerOptions>(builder.Configuration.GetSection(WorldWorkerOptions.SectionName));
builder.Services.Configure<WorldSimulationOptions>(builder.Configuration.GetSection(WorldSimulationOptions.SectionName));
builder.Services.AddLivingRealmsInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorldWorker>();

await builder.Build().RunAsync();
