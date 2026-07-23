using LivingRealms.Infrastructure.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LivingRealms.Worker;

public sealed partial class WorldWorker(
    ILogger<WorldWorker> logger,
    IOptions<WorldWorkerOptions> options,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.IntervalSeconds));
        var startedCentral = CentralNow();
        LogWorkerStarted(logger, interval.TotalSeconds, startedCentral);

        await ProcessWorldAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessWorldAsync(stoppingToken);
        }
    }

    private async Task ProcessWorldAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var simulation = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            var result = await simulation.ProcessOfflineProgressionAsync(DateTimeOffset.UtcNow, stoppingToken);
            var processedCentral = CentralNow();
            LogWorkerProcessed(
                logger,
                result.EventsProcessed,
                result.EventsRecovered,
                result.WorldHoursRequested,
                processedCentral);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogWorkerFailed(logger, exception, CentralNow());
        }
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "World worker started with interval {IntervalSeconds} seconds at {CentralTime}")]
    private static partial void LogWorkerStarted(ILogger logger, double intervalSeconds, DateTimeOffset centralTime);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "World worker processed {EventsProcessed} events, recovered {EventsRecovered}, and requested {WorldHours} world hours at {CentralTime}")]
    private static partial void LogWorkerProcessed(ILogger logger, int eventsProcessed, int eventsRecovered, int worldHours, DateTimeOffset centralTime);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "World worker processing failed at {CentralTime}")]
    private static partial void LogWorkerFailed(ILogger logger, Exception exception, DateTimeOffset centralTime);

    private static DateTimeOffset CentralNow()
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }

        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }
}

public sealed class WorldWorkerOptions
{
    public const string SectionName = "WorldWorker";
    public int IntervalSeconds { get; set; } = 60;
}
