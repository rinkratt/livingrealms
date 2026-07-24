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

public sealed class PhaseSevenBRaidEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseSevenBRaidEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        database.Database.EnsureDeleted();
        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task PlaytestStartCreatesPersistentAttackersAndChangesResidentBehavior()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/world/raid")).StatusCode);

        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/v1/world/raid")).StatusCode);
        await SelectAldenAsync(client, registration);

        var empty = await client.GetFromJsonAsync<RaidStateResponse>("/api/v1/world/raid", JsonOptions);
        Assert.NotNull(empty);
        Assert.False(empty.HasRaid);
        Assert.True(empty.CanStartPlaytest);

        var startedResponse = await client.PostAsync("/api/v1/world/raid/start", null);
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = await startedResponse.Content.ReadFromJsonAsync<RaidStateResponse>(JsonOptions);
        Assert.NotNull(started);
        Assert.True(started.Active);
        Assert.NotNull(started.Raid);
        Assert.Equal("Active", started.Raid.Status);
        Assert.Equal(4, started.Raid.Attackers.Length);
        Assert.All(started.Raid.Attackers, x => Assert.False(x.IsDefeated));

        var residents = await client.GetFromJsonAsync<ResidentResponse[]>(
            "/api/v1/regions/stonehaven-valley/residents",
            JsonOptions);
        Assert.NotNull(residents);
        Assert.Contains(residents, x => x.Name == "Captain Rowan" && x.Activity == "Defending Stonehaven");
        Assert.Contains(residents, x => x.Name == "Brann" && x.Activity == "Holding the reserve line");
        Assert.Contains(residents, x => x.Name == "Elowen" && x.Activity == "Tending wounded defenders");
        Assert.Contains(residents, x => x.Name == "Oren" && x.Activity == "Securing emergency supplies");
        Assert.Contains(residents, x => x.Name == "Nessa" && x.Activity == "Barricading the gate");
        Assert.Contains(residents, x => x.Name == "Dain" && x.Activity == "Reinforcing the walls");

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Single(await database.SettlementRaids.ToListAsync());
        Assert.Equal(4, await database.SettlementRaidAttackers.CountAsync());
        Assert.Equal(4, await database.Creatures.CountAsync(x => x.Role == "Raid Attacker"));
        var launchedRaiders = await database.Creatures
            .Where(x => x.Role == "Raid Attacker")
            .ToArrayAsync();
        Assert.All(launchedRaiders, raider =>
        {
            Assert.InRange(raider.SpawnX, -132.0f, -103.0f);
            Assert.InRange(raider.SpawnZ, -114.0f, -91.0f);
            Assert.InRange(raider.PositionX, -125.0f, -107.0f);
            Assert.InRange(raider.PositionZ, -97.0f, -93.0f);
            Assert.False(string.IsNullOrWhiteSpace(raider.Title));
        });
        Assert.Contains(await database.WorldHistory.ToListAsync(), x => x.EventType == "stonehaven_raid_begun");

        var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
        await simulation.AdvanceActiveRaidAsync(DateTimeOffset.UtcNow.AddMinutes(1), 1, true);
        database.ChangeTracker.Clear();
        var frontLine = await database.SettlementResidents
            .Where(x => x.Role.Contains("Guard"))
            .ToListAsync();
        Assert.Equal(3, frontLine.Count(guard => guard.Health < guard.MaximumHealth));
        var blacksmith = await database.SettlementResidents.SingleAsync(x => x.Role == "Blacksmith");
        Assert.Equal(blacksmith.MaximumHealth, blacksmith.Health);
        var raidAfterOneRound = await database.SettlementRaids.SingleAsync();
        Assert.Equal(SettlementRaidStatus.Active, raidAfterOneRound.Status);
        Assert.Equal(
            await database.SettlementRaidAttackers
                .Where(x => !x.IsDefeated && x.Creature.Status == CreatureStatus.Alive)
                .SumAsync(x => x.Creature.Health),
            raidAfterOneRound.AttackerStrength);
        Assert.Equal(
            await database.SettlementResidents
                .Where(x => x.CanFight && x.Health > 0 && x.Status != ResidentStatus.Dead)
                .SumAsync(x => x.Health),
            raidAfterOneRound.DefenderStrength);
    }

    [Fact]
    public async Task RegularPlayerCannotManuallyStartARaid()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client, administrator: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);

        var state = await client.GetFromJsonAsync<RaidStateResponse>("/api/v1/world/raid", JsonOptions);
        Assert.NotNull(state);
        Assert.False(state.CanStartPlaytest);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Empty(await database.SettlementRaids.ToListAsync());
    }

    [Fact]
    public async Task WoundedFightersRemainOnTheFrontLineUntilTheRaidResolves()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var captain = await database.SettlementResidents.SingleAsync(x => x.Name == "Captain Rowan");
            captain.Health = Math.Max(1, captain.MaximumHealth / 3);
            captain.Status = ResidentStatus.Injured;
            await database.SaveChangesAsync();
        }

        var residents = await client.GetFromJsonAsync<ResidentResponse[]>(
            "/api/v1/regions/stonehaven-valley/residents",
            JsonOptions);

        Assert.NotNull(residents);
        Assert.Contains(residents, x => x.Name == "Captain Rowan" && x.Activity == "Defending Stonehaven");
        Assert.Contains(residents, x => x.Name == "Mara" && x.Activity == "Missing");
    }

    [Fact]
    public async Task HealerAidRestoresActualFrontLineHealthDuringCombat()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var captain = await database.SettlementResidents.SingleAsync(x => x.Name == "Captain Rowan");
        captain.Health = 50;
        captain.Status = ResidentStatus.Injured;
        var attackers = await database.SettlementRaidAttackers.Include(x => x.Creature).ToListAsync();
        foreach (var attacker in attackers)
        {
            attacker.Creature.Attack = 0;
        }
        await database.SaveChangesAsync();

        var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
        await simulation.AdvanceActiveRaidAsync(DateTimeOffset.UtcNow.AddMinutes(1), 1, true);
        database.ChangeTracker.Clear();

        captain = await database.SettlementResidents.SingleAsync(x => x.Name == "Captain Rowan");
        Assert.Equal(42, captain.Health);
        Assert.Equal(ResidentStatus.Injured, captain.Status);
    }

    [Fact]
    public async Task PlayerDefeatsWinTheRaidAndRaidersNeverRespawn()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var characterId = await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
            var attackers = await database.SettlementRaidAttackers
                .Include(x => x.Creature)
                .OrderBy(x => x.Creature.Name)
                .ToListAsync();
            for (var index = 0; index < attackers.Count; index++)
            {
                var contribution = await simulation.RegisterPlayerDefeatAsync(
                    attackers[index].Creature,
                    characterId,
                    DateTimeOffset.UtcNow.AddSeconds(index));
                Assert.NotNull(contribution);
                Assert.True(contribution.ContributionGained > 0);
            }
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var raid = await verification.SettlementRaids.SingleAsync();
        Assert.Equal(SettlementRaidStatus.DefendersWon, raid.Status);
        Assert.True(raid.PlayerContribution >= raid.InitialAttackerStrength);
        Assert.All(
            await verification.Creatures.Where(x => x.Role == "Raid Attacker").ToListAsync(),
            creature =>
            {
                Assert.Equal(CreatureStatus.Retired, creature.Status);
                Assert.Null(creature.RespawnAt);
            });
        Assert.Contains(await verification.WorldHistory.ToListAsync(), x => x.EventType == "stonehaven_raid_repelled");
    }

    [Fact]
    public async Task WoundedSurvivorRetreatsHomeAndCanJoinALaterRaid()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);

        using (var populationScope = _factory.Services.CreateScope())
        {
            var populationDatabase = populationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var faction = await populationDatabase.Factions
                .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
            faction.Population = 12;
            faction.PopulationCapacity = 16;
            await populationDatabase.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);
        Guid survivorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var attackers = await database.SettlementRaidAttackers
                .Include(x => x.Creature)
                .OrderBy(x => x.Creature.Name)
                .ToListAsync();
            var survivor = attackers.First(x => x.Creature.Title == "Clan Raider");
            survivorId = survivor.CreatureId;
            foreach (var attacker in attackers)
            {
                attacker.Creature.Health = attacker.CreatureId == survivorId ? 50 : 1;
            }
            await database.SaveChangesAsync();

            var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
            await simulation.AdvanceActiveRaidAsync(DateTimeOffset.UtcNow.AddMinutes(1), 3, true);
        }

        using (var verificationScope = _factory.Services.CreateScope())
        {
            var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var firstRaid = await verification.SettlementRaids.SingleAsync();
            Assert.Equal(SettlementRaidStatus.DefendersWon, firstRaid.Status);
            var survivor = await verification.Creatures.SingleAsync(x => x.Id == survivorId);
            Assert.Equal(CreatureStatus.Alive, survivor.Status);
            Assert.Equal("Clan Raider", survivor.Role);
            Assert.Equal(survivor.SpawnX, survivor.PositionX);
            Assert.Equal(survivor.SpawnZ, survivor.PositionZ);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);
        using var finalScope = _factory.Services.CreateScope();
        var finalDatabase = finalScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Equal(2, await finalDatabase.SettlementRaids.CountAsync());
        Assert.Equal(
            2,
            await finalDatabase.SettlementRaidAttackers.CountAsync(x => x.CreatureId == survivorId));
    }

    [Fact]
    public async Task UnopposedRaidBreachesStonehavenWithNamedResidentConsequences()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var characterId = await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var preparationDatabase = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            foreach (var defender in await preparationDatabase.SettlementResidents.Where(x => x.CanFight).ToListAsync())
            {
                defender.Health = 1;
            }
            foreach (var attacker in await preparationDatabase.SettlementRaidAttackers.Include(x => x.Creature).ToListAsync())
            {
                attacker.Creature.Health = 1000;
                attacker.Creature.MaximumHealth = 1000;
                attacker.Creature.Attack = 200;
            }
            await preparationDatabase.SaveChangesAsync();

            var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
            await simulation.AdvanceActiveRaidAsync(DateTimeOffset.UtcNow.AddMinutes(1), 24, true);
            await simulation.AdvanceActiveRaidAsync(DateTimeOffset.UtcNow.AddMinutes(2), 24, true);
        }

        using var verificationScope = _factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var raid = await database.SettlementRaids.SingleAsync();
        Assert.Equal(SettlementRaidStatus.AttackersWon, raid.Status);
        Assert.Equal(240, raid.SettlementDamage);
        Assert.Equal(5, raid.ResidentCasualties);
        Assert.True(raid.ResidentInjuries >= 1);
        var settlement = await database.Settlements.SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
        Assert.Equal(760, settlement.StructuralIntegrity);
        Assert.Equal(3, settlement.Population);
        var residents = await database.SettlementResidents.ToListAsync();
        Assert.All(residents.Where(x => x.CanFight), x => Assert.Equal(ResidentStatus.Dead, x.Status));
        Assert.Single(residents, x => !x.CanFight && x.Status == ResidentStatus.Dead);
        Assert.Contains(await database.WorldHistory.ToListAsync(), x => x.EventType == "stonehaven_raid_lost");

        var survivingRaiders = await database.SettlementRaidAttackers
            .Include(x => x.Creature)
            .Where(x => !x.IsDefeated)
            .ToListAsync();
        Assert.NotEmpty(survivingRaiders);
        Assert.All(survivingRaiders, x =>
        {
            Assert.Equal(CreatureStatus.Alive, x.Creature.Status);
            Assert.True(x.Creature.Health > 0);
        });
        var aftermath = await client.GetFromJsonAsync<RaidStateResponse>("/api/v1/world/raid", JsonOptions);
        Assert.NotNull(aftermath);
        Assert.False(aftermath.Active);
        Assert.False(aftermath.CanStartPlaytest);

        var aftermathSimulation = verificationScope.ServiceProvider.GetRequiredService<RaidSimulationService>();
        foreach (var survivor in survivingRaiders)
        {
            var contribution = await aftermathSimulation.RegisterPlayerDefeatAsync(
                survivor.Creature,
                characterId,
                DateTimeOffset.UtcNow);
            Assert.NotNull(contribution);
        }

        database.ChangeTracker.Clear();
        Assert.All(
            await database.Creatures.Where(x => x.Role == "Raid Attacker").ToListAsync(),
            creature => Assert.Equal(CreatureStatus.Retired, creature.Status));
        Assert.Contains(
            await database.WorldHistory.ToListAsync(),
            x => x.EventType == "stonehaven_raid_aftermath_cleared");
        var cleared = await client.GetFromJsonAsync<RaidStateResponse>("/api/v1/world/raid", JsonOptions);
        Assert.NotNull(cleared);
        Assert.True(cleared.CanStartPlaytest);
    }

    [Fact]
    public async Task DevelopmentResetRemovesRaidAndRestoresStonehaven()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        using (var damageScope = _factory.Services.CreateScope())
        {
            var damagedWorld = damageScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var naturalCreature = await damagedWorld.Creatures
                .FirstAsync(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId &&
                                 x.FactionId == null &&
                                 x.Role != "Raid Attacker");
            naturalCreature.Status = CreatureStatus.Dead;
            naturalCreature.Health = 0;
            naturalCreature.RespawnAt = DateTimeOffset.UtcNow.AddHours(1);
            naturalCreature.PositionX = 40;
            naturalCreature.PositionZ = 40;

            var leader = await damagedWorld.Creatures
                .SingleAsync(x => x.Id == LivingRealmsDbContext.GoblinChiefCreatureId);
            leader.Health = 1;
            leader.PositionX = -40;
            leader.PositionZ = -40;

            foreach (var resident in await damagedWorld.SettlementResidents.ToListAsync())
            {
                resident.Health = 0;
                resident.Status = ResidentStatus.Dead;
            }
            await damagedWorld.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/reset", null)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Empty(await database.SettlementRaids.ToListAsync());
        Assert.Empty(await database.SettlementRaidAttackers.ToListAsync());
        Assert.Empty(await database.Creatures.Where(x => x.Role == "Raid Attacker").ToListAsync());
        var worldCreatures = await database.Creatures
            .Where(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId)
            .ToListAsync();
        Assert.All(worldCreatures, creature =>
        {
            Assert.Equal(CreatureStatus.Alive, creature.Status);
            Assert.Equal(creature.MaximumHealth, creature.Health);
            Assert.Null(creature.RespawnAt);
            Assert.Null(creature.LastAttackAt);
            Assert.Equal(creature.SpawnX, creature.PositionX);
            Assert.Equal(creature.SpawnY, creature.PositionY);
            Assert.Equal(creature.SpawnZ, creature.PositionZ);
        });
        var settlement = await database.Settlements
            .Include(x => x.Residents)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
        Assert.Equal(1000, settlement.StructuralIntegrity);
        Assert.Equal(8, settlement.Population);
        Assert.Equal(WorldPopulationService.StartingStonehavenFood, settlement.Food);
        Assert.Equal(WorldPopulationService.StartingStonehavenWood, settlement.Wood);
        Assert.Equal(WorldPopulationService.StartingStonehavenStone, settlement.Stone);
        Assert.Equal(WorldPopulationService.StartingStonehavenIron, settlement.Iron);
        Assert.Equal(8, settlement.Residents.Count(x => x.Status == ResidentStatus.Active));
        Assert.Single(settlement.Residents, x => x.Status == ResidentStatus.Missing);
        Assert.All(settlement.Residents.Where(x => x.Status == ResidentStatus.Active), x =>
        {
            Assert.Equal(x.MaximumHealth, x.Health);
        });
    }

    [Fact]
    public async Task OnlineRaidIsNotDoubleAdvancedByTheAcceleratedWorldWorker()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        await SelectAldenAsync(client, registration);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/world/raid/start", null)).StatusCode);

        int healthBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            healthBefore = await database.SettlementResidents.SumAsync(x => x.Health);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var world = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            await world.AdvanceForTestingAsync(1, DateTimeOffset.UtcNow.AddSeconds(30));
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.Equal(healthBefore, await verification.SettlementResidents.SumAsync(x => x.Health));
        Assert.Equal(SettlementRaidStatus.Active, (await verification.SettlementRaids.SingleAsync()).Status);
    }

    [Fact]
    public async Task LevelThreeCampAutomaticallyStartsAndResolvesStonehavenCounterattack()
    {
        var processedAt = new DateTimeOffset(2026, 7, 17, 18, 0, 0, TimeSpan.Zero);
        using (var populationScope = _factory.Services.CreateScope())
        {
            var database = populationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var settlement = await database.Settlements
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
            settlement.Population = WorldPopulationService.StonehavenAssaultSoldiersRequired;
            var palisade = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId);
            palisade.CurrentLevel = palisade.MaximumLevel;
            palisade.CompletedAt = processedAt;
            await database.SaveChangesAsync();
            var population = populationScope.ServiceProvider.GetRequiredService<WorldPopulationService>();
            await population.EnsureStonehavenResidentsAsync();
        }

        for (var step = 0; step < 4; step++)
        {
            using var scope = _factory.Services.CreateScope();
            var world = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            await world.AdvanceForTestingAsync(24, processedAt.AddMinutes(step));
        }

        using (var activeScope = _factory.Services.CreateScope())
        {
            var database = activeScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var assault = await database.StonehavenAssaults
                .Include(x => x.Members)
                .SingleAsync();
            Assert.Equal(StonehavenAssaultStatus.Assembling, assault.Status);
            Assert.Equal(20, assault.Members.Count);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var world = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            await world.AdvanceForTestingAsync(48, processedAt.AddMinutes(1));
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var world = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            await world.AdvanceForTestingAsync(48, processedAt.AddMinutes(2));
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var assaultResult = await verification.StonehavenAssaults.SingleAsync();
        Assert.DoesNotContain(assaultResult.Status, new[]
        {
            StonehavenAssaultStatus.Assembling,
            StonehavenAssaultStatus.Marching,
            StonehavenAssaultStatus.FightingGoblins,
            StonehavenAssaultStatus.AttackingCamp
        });
        Assert.NotNull(assaultResult.ResolvedAt);
        Assert.Contains(
            await verification.WorldHistory.ToListAsync(),
            x => x.EventType is "stonehaven_counterattack_won" or "stonehaven_counterattack_lost");
    }

    [Fact]
    public async Task FifteenRaidReadyGoblinsAutomaticallyLaunchOnStonehaven()
    {
        var processedAt = new DateTimeOffset(2026, 7, 17, 18, 0, 0, TimeSpan.Zero);
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var faction = await database.Factions.SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        faction.Population = 16;
        faction.PopulationCapacity = 16;
        faction.DevelopmentStage = 2;
        await database.SaveChangesAsync();

        var population = scope.ServiceProvider.GetRequiredService<WorldPopulationService>();
        await population.EnsureDarkwoodClanMembersAsync();
        var simulation = scope.ServiceProvider.GetRequiredService<RaidSimulationService>();
        await simulation.EvaluateWorldProgressionAsync(1, processedAt);

        var raid = await database.SettlementRaids
            .Include(x => x.Attackers)
            .SingleAsync();
        Assert.Equal(SettlementRaidStatus.Active, raid.Status);
        Assert.Equal(15, raid.Attackers.Count);
    }

    private async Task<AuthenticationResponse> RegisterAsync(HttpClient client, bool administrator = true)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"phase7b-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = Assert.IsType<AuthenticationResponse>(
            await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions));
        if (administrator)
        {
            using var scope = _factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var account = await database.Accounts.SingleAsync(x => x.Id == registration.Account.Id);
            account.IsAdministrator = true;
            await database.SaveChangesAsync();
        }
        return registration;
    }

    private static async Task<Guid> SelectAldenAsync(HttpClient client, AuthenticationResponse registration)
    {
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);
        return alden.Id;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record AuthenticationResponse(string Token, AccountResponse Account, CharacterResponse[] Characters);
    private sealed record AccountResponse(Guid Id, string Email, bool IsAdministrator);
    private sealed record CharacterResponse(Guid Id, string Name);
    private sealed record RaidStateResponse(
        bool HasRaid,
        bool Active,
        bool CanStartPlaytest,
        RaidResponse? Raid,
        DateTimeOffset ServerTimeCentral);
    private sealed record RaidResponse(
        Guid Id,
        string Status,
        int WorldDay,
        int InitialAttackerStrength,
        int AttackerStrength,
        int InitialDefenderStrength,
        int DefenderStrength,
        int PlayerContribution,
        int SettlementDamage,
        int ResidentCasualties,
        int ResidentInjuries,
        string? OutcomeSummary,
        RaidAttackerResponse[] Attackers);
    private sealed record RaidAttackerResponse(
        Guid CreatureId,
        string Name,
        int Level,
        int Health,
        int MaximumHealth,
        string Status,
        bool IsDefeated,
        bool DefeatedByPlayer);
    private sealed record ResidentResponse(string Name, string Activity);
}
