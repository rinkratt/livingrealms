using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseSixEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseSixEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        database.Database.EnsureDeleted();
        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task DevelopmentAdvanceGrowsFactionUpgradesCampPromotesLeaderAndRecordsHistory()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);

        var initial = await client.GetFromJsonAsync<WorldStateResponse>("/api/v1/world/state", JsonOptions);
        Assert.NotNull(initial);
        Assert.True(initial.CanAccelerate);
        Assert.Equal(0, initial.SimulatedHours);
        Assert.Equal(1, initial.WorldDay);
        Assert.Equal("Darkwood Clan", initial.Faction.Name);
        Assert.Equal(7, initial.Faction.Population);
        Assert.Equal(1, initial.Faction.DevelopmentStage);
        Assert.Equal("Encampment", initial.Faction.StageName);
        Assert.Equal("Goblin Chief", initial.Faction.Leader.Title);
        Assert.Equal(5, initial.Faction.Resources.Count);
        Assert.Equal("Stonehaven Village", initial.Settlement.Name);
        Assert.Equal(8, initial.Settlement.Population);
        Assert.Equal(8, initial.Settlement.LivingResidents);
        Assert.Equal(24, initial.Settlement.HousingCapacity);
        Assert.Equal(64, initial.Settlement.Food);
        Assert.Equal(40, initial.Settlement.Wood);
        Assert.Equal(24, initial.Settlement.Stone);
        Assert.Equal(4, initial.Settlement.Iron);
        Assert.Equal("Captain Rowan", initial.Settlement.Leader.Name);
        Assert.Equal("Warden of Stonehaven", initial.Settlement.Leader.Title);
        Assert.Equal(6, initial.EventReadiness.DarkwoodRaid.Current);
        Assert.Equal(15, initial.EventReadiness.DarkwoodRaid.Required);
        Assert.Equal(8, initial.EventReadiness.StonehavenCounterattack.Current);
        Assert.Equal(20, initial.EventReadiness.StonehavenCounterattack.Required);
        Assert.Contains(initial.RecentHistory, x => x.EventType == "faction_founded");

        var advancedResponse = await client.PostAsJsonAsync("/api/v1/world/advance", new AdvanceWorldRequest(24));
        Assert.Equal(HttpStatusCode.OK, advancedResponse.StatusCode);
        var advanced = await advancedResponse.Content.ReadFromJsonAsync<AdvanceWorldResponse>(JsonOptions);
        Assert.NotNull(advanced);
        using (var diagnosticScope = _factory.Services.CreateScope())
        {
            var diagnosticDatabase = diagnosticScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var persistedHours = await diagnosticDatabase.Factions
                .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
                .Select(x => x.SimulatedHours)
                .SingleAsync();
            var persistedEvents = await diagnosticDatabase.ScheduledEvents.ToListAsync();
            Assert.Single(persistedEvents);
            Assert.Contains("\"worldHours\":24", persistedEvents[0].PayloadJson, StringComparison.Ordinal);
            Assert.Single(await diagnosticDatabase.WorldHistory.Where(x => x.EventType == "faction_progressed").ToListAsync());
            Assert.Equal(24, persistedHours);

            var projects = await diagnosticDatabase.ConstructionProjects
                .Where(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId ||
                            x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId)
                .ToDictionaryAsync(x => x.Id);
            var wall = projects[LivingRealmsDbContext.StonehavenWallProjectId];
            Assert.True(wall.WoodContributed > 0);
            Assert.True(wall.StoneContributed > 0);
            Assert.NotNull(wall.LastNpcContributionAt);
            var palisade = projects[LivingRealmsDbContext.DarkwoodPalisadeProjectId];
            Assert.True(palisade.WoodContributed > 0);
            Assert.True(palisade.StoneContributed > 0);
            Assert.NotNull(palisade.LastNpcContributionAt);

            Assert.Contains(
                await diagnosticDatabase.SettlementResidents
                    .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId)
                    .ToListAsync(),
                x => x.Name == "Aveline Hart" &&
                     x.Role == "Miner" &&
                     x.Status == ResidentStatus.Active);
            Assert.Contains(
                await diagnosticDatabase.ResourceContributions.ToListAsync(),
                x => x.Source == "WorldSimulation" && x.ContributorName == "Nessa");
            Assert.Contains(
                await diagnosticDatabase.ResourceContributions.ToListAsync(),
                x => x.Source == "WorldSimulation" && x.ContributorName == "Skrit");
        }
        Assert.Equal(1, advanced.Run.EventsProcessed);
        Assert.Equal(24, advanced.World.SimulatedHours);
        Assert.Equal(2, advanced.World.WorldDay);
        Assert.Equal(9, advanced.World.Faction.Population);
        Assert.Equal(2, advanced.World.Faction.DevelopmentStage);
        Assert.Equal("Established Camp", advanced.World.Faction.StageName);
        Assert.Equal("Goblin Chieftain", advanced.World.Faction.Leader.Title);
        Assert.Equal(9, advanced.World.Faction.Leader.Level);
        Assert.Equal(204, advanced.World.Faction.Leader.MaximumHealth);
        Assert.Equal(25, advanced.World.Faction.Leader.Attack);
        Assert.Equal(16, advanced.World.Faction.Leader.Defense);
        Assert.Equal(9, advanced.World.Settlement.Population);
        Assert.Equal(9, advanced.World.Settlement.LivingResidents);
        Assert.Equal(80, advanced.World.Settlement.Food);
        Assert.Equal(44, advanced.World.Settlement.Wood);
        Assert.Equal(36, advanced.World.Settlement.Stone);
        Assert.Equal(14, advanced.World.Settlement.Iron);
        Assert.Contains(advanced.World.RecentHistory, x => x.EventType == "stonehaven_population_growth");
        Assert.Contains(advanced.World.Faction.Structures, x => x.Name == "Timber Palisade");
        Assert.Contains(advanced.World.Faction.Structures, x => x.Name == "Hunter Lodge");
        Assert.Equal(0, advanced.World.Events.Pending);
        Assert.Equal(1, advanced.World.Events.Completed);
        Assert.Contains(advanced.World.RecentHistory, x => x.EventType == "camp_upgrade");
        Assert.Contains(advanced.World.RecentHistory, x => x.EventType == "leader_promoted");

        var history = await client.GetFromJsonAsync<HistoryResponse[]>("/api/v1/world/history?limit=20", JsonOptions);
        Assert.NotNull(history);
        Assert.Contains(history, x => x.EventType == "faction_progressed");
        Assert.Contains(history, x => x.Title.Contains("Darkwood", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfflineProcessingRecoversInterruptedEventAndDoesNotRepeatCompletedElapsedTime()
    {
        var now = new DateTimeOffset(2026, 7, 17, 16, 0, 0, TimeSpan.Zero);
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var faction = await database.Factions.SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
            faction.LastProcessedAt = now.AddMinutes(-120);
            database.ScheduledEvents.Add(new ScheduledEvent
            {
                EventType = WorldSimulationService.ProgressionEventType,
                TargetId = LivingRealmsDbContext.DarkwoodClanId,
                ScheduledAt = now.AddMinutes(-8),
                Status = ScheduledEventStatus.Processing,
                StartedAt = now.AddMinutes(-7),
                IdempotencyKey = $"interrupted-test-{Guid.NewGuid():N}",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    worldHours = 1,
                    processedAt = now.AddMinutes(-8),
                    source = "recovery-test"
                }, JsonOptions)
            });
            await database.SaveChangesAsync();
        }

        WorldSimulationRunResult firstRun;
        using (var scope = _factory.Services.CreateScope())
        {
            var simulation = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            firstRun = await simulation.ProcessOfflineProgressionAsync(now);
        }
        Assert.Equal(1, firstRun.EventsRecovered);
        Assert.Equal(2, firstRun.EventsProcessed);
        Assert.Equal(2, firstRun.WorldHoursRequested);

        WorldSimulationRunResult secondRun;
        using (var scope = _factory.Services.CreateScope())
        {
            var simulation = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            secondRun = await simulation.ProcessOfflineProgressionAsync(now);
        }
        Assert.Equal(0, secondRun.EventsProcessed);
        Assert.Equal(0, secondRun.WorldHoursRequested);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.DoesNotContain(
            await verificationDatabase.ScheduledEvents.ToListAsync(),
            x => x.Status is ScheduledEventStatus.Pending or ScheduledEventStatus.Processing);
    }

    [Fact]
    public async Task DevelopmentResetRestoresInitialFactionWithoutResettingPlayerAccount()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/world/advance", new AdvanceWorldRequest(96))).StatusCode);

        var resetResponse = await client.PostAsync("/api/v1/world/reset", null);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var reset = await resetResponse.Content.ReadFromJsonAsync<WorldStateResponse>(JsonOptions);
        Assert.NotNull(reset);
        Assert.Equal(0, reset.SimulatedHours);
        Assert.Equal(1, reset.WorldDay);
        Assert.Equal(7, reset.Faction.Population);
        Assert.Equal(10, reset.Faction.PopulationCapacity);
        Assert.Equal(1, reset.Faction.DevelopmentStage);
        Assert.Equal("Encampment", reset.Faction.StageName);
        Assert.Equal(8, reset.Faction.Leader.Level);
        Assert.Equal("Goblin Chief", reset.Faction.Leader.Title);
        Assert.Equal(180, reset.Faction.Leader.MaximumHealth);
        Assert.Equal(22, reset.Faction.Leader.Attack);
        Assert.Equal(14, reset.Faction.Leader.Defense);
        Assert.Equal(2, reset.Faction.Structures.Count);
        Assert.Equal(8, reset.Settlement.Population);
        Assert.Equal(8, reset.Settlement.LivingResidents);
        Assert.Equal(0, reset.Events.Pending);
        Assert.Equal(0, reset.Events.Completed);
        Assert.Equal(0, reset.Events.Failed);
        Assert.Contains(reset.RecentHistory, x => x.EventType == "playtest_reset");

        using var verificationScope = _factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Empty(await database.ScheduledEvents.ToListAsync());
        Assert.Equal(2, await database.FactionStructures.CountAsync());
        Assert.Equal(2, await database.Characters.CountAsync());
    }

    [Fact]
    public async Task RegularPlayerCanReadChroniclesButCannotControlTheWorld()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client, administrator: false);

        var state = await client.GetFromJsonAsync<WorldStateResponse>("/api/v1/world/state", JsonOptions);
        Assert.NotNull(state);
        Assert.False(state.CanAccelerate);
        Assert.NotEmpty(state.RecentHistory);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/world/advance", new AdvanceWorldRequest(24))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync("/api/v1/world/reset", null)).StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Equal(
            0,
            await database.Factions
                .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
                .Select(x => x.SimulatedHours)
                .SingleAsync());
    }

    private async Task RegisterAsync(HttpClient client, bool administrator = true)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"phase6-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        Assert.NotNull(registration);
        if (administrator)
        {
            using var scope = _factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var account = await database.Accounts.SingleAsync(x => x.Id == registration.Account.Id);
            account.IsAdministrator = true;
            await database.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record AuthenticationResponse(string Token, AccountResponse Account);
    private sealed record AccountResponse(Guid Id, string Email, bool IsAdministrator);
    private sealed record AdvanceWorldRequest(int Hours);
    private sealed record AdvanceWorldResponse(WorldSimulationRunResult Run, WorldStateResponse World);
    private sealed record WorldStateResponse(
        long SimulatedHours,
        int WorldDay,
        string SimulationSpeed,
        bool CanAccelerate,
        FactionResponse Faction,
        SettlementResponse Settlement,
        EventReadinessResponse EventReadiness,
        EventQueueResponse Events,
        IReadOnlyCollection<HistoryResponse> RecentHistory);
    private sealed record FactionResponse(
        string Name,
        int Population,
        int PopulationCapacity,
        int DevelopmentStage,
        string StageName,
        IReadOnlyCollection<ResourceResponse> Resources,
        IReadOnlyCollection<StructureResponse> Structures,
        LeaderResponse Leader);
    private sealed record ResourceResponse(string Kind, long Amount, long Capacity);
    private sealed record StructureResponse(string Name, int Level, int Health);
    private sealed record LeaderResponse(
        string Name,
        string Title,
        int Level,
        int MaximumHealth,
        int Attack,
        int Defense);
    private sealed record SettlementResponse(
        string Name,
        int Population,
        int LivingResidents,
        int CombatReadyResidents,
        int HousingCapacity,
        int Food,
        int Wood,
        int Stone,
        int Iron,
        int DefenseRating,
        int GuardStrength,
        SettlementLeaderResponse Leader);
    private sealed record SettlementLeaderResponse(
        string Name,
        string Title,
        string Role,
        int Health,
        int MaximumHealth,
        string Status);
    private sealed record EventReadinessResponse(
        TriggerReadinessResponse DarkwoodRaid,
        TriggerReadinessResponse StonehavenCounterattack);
    private sealed record TriggerReadinessResponse(
        string Name,
        int Current,
        int Required,
        bool Active,
        string Explanation);
    private sealed record EventQueueResponse(int Pending, int Completed, int Failed);
    private sealed record HistoryResponse(Guid Id, string EventType, string Title, string Description, int ImportanceLevel, DateTimeOffset OccurredAtCentral);
}
