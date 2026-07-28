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
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, initial.Settlement.Population);
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, initial.Settlement.LivingResidents);
        Assert.Equal(24, initial.Settlement.HousingCapacity);
        Assert.Equal(64, initial.Settlement.Food);
        Assert.Equal(40, initial.Settlement.Wood);
        Assert.Equal(24, initial.Settlement.Stone);
        Assert.Equal(4, initial.Settlement.Iron);
        Assert.Equal("Reeve Aldric Vale", initial.Settlement.Leader.Name);
        Assert.Equal("Reeve of Stonehaven", initial.Settlement.Leader.Title);
        Assert.Equal(2, initial.Survival.Stonehaven.Farmers);
        Assert.Equal(1, initial.Survival.Stonehaven.Fishers);
        Assert.Equal(13, initial.Survival.Stonehaven.FoodProducedPerHour);
        Assert.Equal(11, initial.Survival.Stonehaven.FoodConsumedPerHour);
        Assert.Equal(2, initial.Survival.Stonehaven.NetFoodPerHour);
        Assert.Equal(1, initial.Survival.Darkwood.Hunters);
        Assert.Equal(10, initial.Survival.Darkwood.FoodProducedPerHour);
        Assert.Equal(15, initial.Survival.Wildlife.Total);
        Assert.Equal(15, initial.Survival.Wildlife.Available);
        Assert.Equal(6, initial.EventReadiness.DarkwoodRaid.Current);
        Assert.Equal(15, initial.EventReadiness.DarkwoodRaid.Required);
        Assert.False(initial.EventReadiness.DarkwoodRaid.Ready);
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, initial.EventReadiness.StonehavenCounterattack.Current);
        Assert.Equal(20, initial.EventReadiness.StonehavenCounterattack.Required);
        Assert.False(initial.EventReadiness.StonehavenCounterattack.Ready);
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
            Assert.True(
                wall.StoneContributed > 0,
                $"Stonehaven wall stone contribution was {wall.StoneContributed}; store has " +
                $"{await diagnosticDatabase.Settlements.Where(x => x.Id == LivingRealmsDbContext.StonehavenVillageId).Select(x => x.Stone).SingleAsync()} stone.");
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
                     x.Role == "Farmer" &&
                     x.Status == ResidentStatus.Active);
            Assert.Contains(
                await diagnosticDatabase.SettlementResidents
                    .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId)
                    .ToListAsync(),
                x => x.Role == "Hunter" &&
                     x.Status is ResidentStatus.Active or ResidentStatus.Injured);
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
        Assert.Equal(8, advanced.World.Faction.Population);
        Assert.Equal(2, advanced.World.Faction.DevelopmentStage);
        Assert.Equal("Established Camp", advanced.World.Faction.StageName);
        Assert.Equal("Goblin Chieftain", advanced.World.Faction.Leader.Title);
        Assert.Equal(9, advanced.World.Faction.Leader.Level);
        Assert.Equal(204, advanced.World.Faction.Leader.MaximumHealth);
        Assert.Equal(25, advanced.World.Faction.Leader.Attack);
        Assert.Equal(19, advanced.World.Faction.Leader.Defense);
        Assert.Equal(15, advanced.World.Settlement.Population);
        Assert.Equal(15, advanced.World.Settlement.LivingResidents);
        Assert.Equal(120, advanced.World.Settlement.Food);
        Assert.Equal(16, advanced.World.Settlement.Wood);
        Assert.Equal(16, advanced.World.Settlement.Stone);
        Assert.Equal(6, advanced.World.Settlement.Iron);
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
    public async Task DefeatedFactionLeaderStaysDeadAndARecordedSuccessorTakesCommand()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var population = scope.ServiceProvider.GetRequiredService<WorldPopulationService>();
        var leadership = scope.ServiceProvider.GetRequiredService<FactionLeadershipService>();
        await population.EnsureDarkwoodClanMembersAsync();

        var gorvak = await database.Creatures
            .Include(x => x.Species)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.GoblinChiefCreatureId);
        gorvak.Health = 0;
        var result = await leadership.ResolvePersistentDefeatAsync(
            gorvak,
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        await database.SaveChangesAsync();

        Assert.True(result.LeadershipChanged);
        Assert.NotNull(result.Successor);
        Assert.NotEqual(gorvak.Id, result.Successor.Id);
        Assert.Equal(CreatureStatus.Dead, gorvak.Status);
        Assert.Null(gorvak.RespawnAt);
        var faction = await database.Factions.SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        Assert.Equal(result.Successor.Id, faction.LeaderCreatureId);
        Assert.Contains(
            await database.WorldHistory.ToListAsync(),
            x => x.EventType == "faction_leadership_succession" &&
                 x.Description.Contains(gorvak.Name));
    }

    [Fact]
    public async Task NamedHuntingPartiesContestPersistentWildlifeAfterStonehavenRecruitsAHunter()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/world/advance", new AdvanceWorldRequest(24))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/world/advance", new AdvanceWorldRequest(6))).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var hunter = await database.SettlementResidents
            .SingleAsync(x => x.Name == "Garran Holt");
        Assert.Equal("Hunter", hunter.Role);
        Assert.Equal(ResidentStatus.Injured, hunter.Status);
        Assert.True(hunter.Health < hunter.MaximumHealth);

        var darkwoodHunter = await database.Creatures
            .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                        x.Role == "Clan Hunter")
            .OrderBy(x => x.Id)
            .FirstAsync();
        Assert.True(darkwoodHunter.Health < darkwoodHunter.MaximumHealth);
        Assert.InRange(
            await database.Creatures.CountAsync(x =>
                x.FactionId == null &&
                x.Status == CreatureStatus.Dead &&
                x.RespawnAt != null &&
                (x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId ||
                 x.SpeciesId == LivingRealmsDbContext.PrairieWolfSpeciesId)),
            3,
            5);
        Assert.Contains(
            await database.WorldHistory.ToListAsync(),
            x => x.EventType == "hunting_skirmish" &&
                 x.Description.Contains(hunter.Name, StringComparison.Ordinal));
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
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, reset.Settlement.Population);
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, reset.Settlement.LivingResidents);
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

    [Fact]
    public async Task PhaseFiveMakesA3TheOnlyPersistentIronSourceAndPaysNamedMineGuards()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/reset", null)).StatusCode);

        var initial = await client.GetFromJsonAsync<WorldStateResponse>("/api/v1/world/state", JsonOptions);
        Assert.NotNull(initial);
        Assert.Equal("A3", initial.IronEconomy.Source.Grid);
        Assert.Equal(initial.IronEconomy.Source.Capacity, initial.IronEconomy.Source.Remaining);
        Assert.Equal("Dain", initial.IronEconomy.Stonehaven.MinerName);
        Assert.Equal(0, initial.IronEconomy.Stonehaven.TripsCompleted);
        Assert.Equal(0, initial.IronEconomy.Darkwood.TripsCompleted);

        var advancedResponse = await client.PostAsJsonAsync(
            "/api/v1/world/advance",
            new AdvanceWorldRequest(24));
        Assert.Equal(HttpStatusCode.OK, advancedResponse.StatusCode);
        var advanced = await advancedResponse.Content.ReadFromJsonAsync<AdvanceWorldResponse>(JsonOptions);
        Assert.NotNull(advanced);
        var iron = advanced.World.IronEconomy;
        Assert.True(iron.Source.Remaining < iron.Source.Capacity);
        Assert.True(iron.Stonehaven.TripsCompleted > 0);
        Assert.True(iron.Darkwood.TripsCompleted > 0);
        Assert.True(iron.Stonehaven.TotalIronDelivered > 0);
        Assert.True(iron.Darkwood.TotalIronDelivered > 0);
        Assert.True(iron.Stonehaven.WeaponTier + iron.Stonehaven.ArmorTier > 0);
        Assert.True(iron.Darkwood.WeaponTier + iron.Darkwood.ArmorTier > 0);
        Assert.Equal(2, iron.StonehavenMineGuards.Count);
        Assert.Equal(10, iron.StonehavenMineGuards.CurrentDailyCost);
        Assert.Equal(2, iron.StonehavenMineGuards.Names.Count);
        Assert.True(iron.StonehavenMineGuards.TreasuryGold >= 20);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Equal(2, await database.IronMiningOperations.CountAsync());
        Assert.Equal(2, await database.SettlementResidents.CountAsync(x =>
            x.Role == "A3 Mine Guard" &&
            x.Status == ResidentStatus.Active));
        Assert.Contains(await database.WorldHistory.ToListAsync(), x => x.EventType == "iron_delivered");
        Assert.Contains(await database.WorldHistory.ToListAsync(), x => x.EventType == "iron_equipment_upgraded");
        Assert.Contains(await database.WorldHistory.ToListAsync(), x => x.EventType == "irondeep_guard_contract");
    }

    [Fact]
    public async Task FoodShortagesRecruitWorkersUntilBothSettlementsCanGrowSustainably()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var population = scope.ServiceProvider.GetRequiredService<WorldPopulationService>();

        await population.EnsureHuntableWildlifeAsync();
        await population.EnsureStonehavenResidentsAsync();
        var settlement = await database.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
        var residents = await database.SettlementResidents
            .Where(x => x.SettlementId == settlement.Id &&
                        x.Health > 0 &&
                        (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured))
            .ToListAsync();
        foreach (var extraFarmer in residents
                     .Where(x => x.Role == "Farmer")
                     .Skip(1))
        {
            extraFarmer.Role = "Weaver";
        }

        var faction = await database.Factions
            .Include(x => x.Resources)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        faction.Population = 15;
        faction.PopulationCapacity = 16;
        await database.SaveChangesAsync();
        await population.EnsureDarkwoodClanMembersAsync();
        var clanMembers = await database.Creatures
            .Where(x => x.FactionId == faction.Id &&
                        x.Health > 0 &&
                        x.Status == CreatureStatus.Alive)
            .ToListAsync();
        foreach (var extraHunter in clanMembers
                     .Where(x => x.Role == "Clan Hunter")
                     .Skip(1))
        {
            extraHunter.Role = "Clan Raider";
            extraHunter.Title = "Clan Raider";
        }
        await database.SaveChangesAsync();

        var initialStonehaven = WorldSurvivalService.CalculateStonehaven(
            residents,
            settlement.Food,
            15);
        var initialDarkwood = WorldSurvivalService.CalculateDarkwood(
            clanMembers,
            faction.Resources.Single(x => x.Kind == ResourceKind.Food).Amount,
            15);
        Assert.True(initialStonehaven.IsShortage);
        Assert.True(initialDarkwood.IsShortage);

        var firstRecovery = await population.RecruitFoodWorkersForSustainabilityAsync();
        Assert.NotNull(firstRecovery.Stonehaven);
        Assert.Equal("Farmer", firstRecovery.Stonehaven.Role);
        Assert.NotNull(firstRecovery.Darkwood);
        Assert.Equal("Clan Hunter", firstRecovery.Darkwood.Role);

        var secondRecovery = await population.RecruitFoodWorkersForSustainabilityAsync();
        Assert.NotNull(secondRecovery.Stonehaven);
        Assert.Equal("Hunter", secondRecovery.Stonehaven.Role);
        Assert.Null(secondRecovery.Darkwood);

        residents = await database.SettlementResidents
            .Where(x => x.SettlementId == settlement.Id &&
                        x.Health > 0 &&
                        (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured))
            .ToListAsync();
        clanMembers = await database.Creatures
            .Where(x => x.FactionId == faction.Id &&
                        x.Health > 0 &&
                        x.Status == CreatureStatus.Alive)
            .ToListAsync();
        var recoveredStonehaven = WorldSurvivalService.CalculateStonehaven(
            residents,
            settlement.Food,
            15);
        var recoveredDarkwood = WorldSurvivalService.CalculateDarkwood(
            clanMembers,
            faction.Resources.Single(x => x.Kind == ResourceKind.Food).Amount,
            15);

        Assert.False(recoveredStonehaven.IsShortage);
        Assert.True(
            recoveredStonehaven.NetFoodPerHour >= WorldSurvivalService.TargetFoodSurplusPerHour);
        Assert.False(recoveredDarkwood.IsShortage);
        Assert.True(
            recoveredDarkwood.NetFoodPerHour >= WorldSurvivalService.TargetFoodSurplusPerHour);
        Assert.Equal(13, residents.Count);
        Assert.Equal(16, clanMembers.Count);
    }

    [Fact]
    public async Task PhaseSixBanksBeginEmptyBuyOnlyRealSurplusAndResellTheirOwnInventory()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/reset", null)).StatusCode);

        var initial = await client.GetFromJsonAsync<WorldStateResponse>("/api/v1/world/state", JsonOptions);
        Assert.NotNull(initial);
        Assert.Equal(300, initial.Banks.Stonehaven.BankGold);
        Assert.Equal(300, initial.Banks.Darkwood.BankGold);
        Assert.All(initial.Banks.Stonehaven.Inventory, x => Assert.Equal(0, x.BankQuantity));
        Assert.All(initial.Banks.Darkwood.Inventory, x => Assert.Equal(0, x.BankQuantity));
        Assert.Empty(initial.Banks.Stonehaven.RecentTransactions);
        Assert.Empty(initial.Banks.Darkwood.RecentTransactions);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var settlement = await database.Settlements
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
            settlement.Wood = 240;
            settlement.Stone = 180;
            var resources = await database.FactionResources
                .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId)
                .ToDictionaryAsync(x => x.Kind);
            resources[ResourceKind.Wood].Amount = 220;
            resources[ResourceKind.Stone].Amount = 180;
            await database.SaveChangesAsync();
        }

        var saleResponse = await client.PostAsJsonAsync(
            "/api/v1/world/advance",
            new AdvanceWorldRequest(1));
        Assert.Equal(HttpStatusCode.OK, saleResponse.StatusCode);
        var afterSales = await saleResponse.Content.ReadFromJsonAsync<AdvanceWorldResponse>(JsonOptions);
        Assert.NotNull(afterSales);
        var stonehavenWood = afterSales.World.Banks.Stonehaven.Inventory.Single(x => x.Kind == "Wood");
        var darkwoodWood = afterSales.World.Banks.Darkwood.Inventory.Single(x => x.Kind == "Wood");
        Assert.True(stonehavenWood.BankQuantity > 0);
        Assert.True(darkwoodWood.BankQuantity > 0);
        Assert.True(afterSales.World.Banks.Stonehaven.FactionGold > 30);
        Assert.True(afterSales.World.Banks.Darkwood.FactionGold > 0);
        Assert.Contains(
            afterSales.World.Banks.Stonehaven.RecentTransactions,
            x => x.Type == "FactionSold" && x.Kind == "Wood");

        var bankWoodBeforeBuy = stonehavenWood.BankQuantity;
        var treasuryBeforeBuy = afterSales.World.Banks.Stonehaven.FactionGold;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var settlement = await database.Settlements
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
            settlement.Wood = 0;
            await database.SaveChangesAsync();
        }

        var buyResponse = await client.PostAsJsonAsync(
            "/api/v1/world/advance",
            new AdvanceWorldRequest(1));
        Assert.Equal(HttpStatusCode.OK, buyResponse.StatusCode);
        var afterBuy = await buyResponse.Content.ReadFromJsonAsync<AdvanceWorldResponse>(JsonOptions);
        Assert.NotNull(afterBuy);
        var stonehavenWoodAfterBuy = afterBuy.World.Banks.Stonehaven.Inventory.Single(x => x.Kind == "Wood");
        Assert.True(stonehavenWoodAfterBuy.BankQuantity < bankWoodBeforeBuy);
        var woodPurchase = Assert.Single(
            afterBuy.World.Banks.Stonehaven.RecentTransactions,
            x => x.Type == "FactionBought" && x.Kind == "Wood");
        Assert.True(woodPurchase.FactionGoldAfter < treasuryBeforeBuy);
        Assert.Contains(
            await ReadHistoryAsync(),
            x => x.EventType == "bank_trade");

        var resetResponse = await client.PostAsync("/api/v1/world/reset", null);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var reset = await resetResponse.Content.ReadFromJsonAsync<WorldStateResponse>(JsonOptions);
        Assert.NotNull(reset);
        Assert.All(reset.Banks.Stonehaven.Inventory, x => Assert.Equal(0, x.BankQuantity));
        Assert.All(reset.Banks.Darkwood.Inventory, x => Assert.Equal(0, x.BankQuantity));
        Assert.Empty(reset.Banks.Stonehaven.RecentTransactions);
        Assert.Empty(reset.Banks.Darkwood.RecentTransactions);
        Assert.Equal(300, reset.Banks.Stonehaven.BankGold);
        Assert.Equal(300, reset.Banks.Darkwood.BankGold);

        async Task<List<WorldHistory>> ReadHistoryAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            return await database.WorldHistory.ToListAsync();
        }
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
        SurvivalResponse Survival,
        IronEconomyResponse IronEconomy,
        FactionBanksResponse Banks,
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
    private sealed record SurvivalResponse(
        FoodEconomyResponse Stonehaven,
        FoodEconomyResponse Darkwood,
        WildlifeResponse Wildlife);
    private sealed record FoodEconomyResponse(
        int Population,
        int FoodStored,
        int Farmers,
        int Fishers,
        int Hunters,
        int FoodProducedPerHour,
        int FoodConsumedPerHour,
        int NetFoodPerHour,
        bool IsShortage,
        int HoursOfFoodRemaining,
        string RecommendedRecruitmentRole);
    private sealed record WildlifeResponse(int Total, int Available, int Respawning);
    private sealed record IronEconomyResponse(
        IronSourceResponse Source,
        IronOperationResponse Stonehaven,
        IronOperationResponse Darkwood,
        MineGuardResponse StonehavenMineGuards);
    private sealed record IronSourceResponse(
        string Grid,
        string Name,
        int Remaining,
        int Capacity,
        int MineHealth,
        int MineMaximumHealth,
        bool Operational);
    private sealed record IronOperationResponse(
        string Owner,
        string MinerName,
        string Status,
        int CargoIron,
        int TotalIronDelivered,
        int TripsCompleted,
        long StoredIron,
        int WeaponTier,
        int? NextWeaponTierCost,
        int ArmorTier,
        int? NextArmorTierCost);
    private sealed record MineGuardResponse(
        int Count,
        int GoldPerGuardPerWorldDay,
        int CurrentDailyCost,
        int TreasuryGold,
        IReadOnlyCollection<string> Names);
    private sealed record FactionBanksResponse(
        FactionBankResponse Stonehaven,
        FactionBankResponse Darkwood);
    private sealed record FactionBankResponse(
        string Owner,
        string Name,
        int BankGold,
        int FactionGold,
        IReadOnlyCollection<BankInventoryResponse> Inventory,
        IReadOnlyCollection<BankTransactionResponse> RecentTransactions);
    private sealed record BankInventoryResponse(
        string Kind,
        int BankQuantity,
        int BankBuyPrice,
        int BankSellPrice,
        long FactionStored,
        int TargetReserve,
        long Shortage);
    private sealed record BankTransactionResponse(
        string Type,
        string Kind,
        int Quantity,
        int UnitPrice,
        int TotalGold,
        int BankGoldAfter,
        int FactionGoldAfter,
        string Description,
        DateTimeOffset OccurredAtCentral);
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
        bool Ready,
        bool Active,
        bool AdministratorOnline,
        string Explanation);
    private sealed record EventQueueResponse(int Pending, int Completed, int Failed);
    private sealed record HistoryResponse(Guid Id, string EventType, string Title, string Description, int ImportanceLevel, DateTimeOffset OccurredAtCentral);
}
