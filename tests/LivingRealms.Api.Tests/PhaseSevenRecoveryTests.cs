using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseSevenRecoveryTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseSevenRecoveryTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        database.Database.EnsureDeleted();
        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task StonehavenWaitsFifteenRealMinutesThenFoundersRebuildFunctionsBeforeWalls()
    {
        var defeatedAt = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var recovery = scope.ServiceProvider.GetRequiredService<SettlementRecoveryService>();

        await recovery.MarkDefeatedAsync(ResourceOwner.Stonehaven, defeatedAt);
        var defeated = Assert.Single(
            await recovery.GetStatesAsync(defeatedAt.AddMinutes(14).AddSeconds(59)),
            x => x.Owner == nameof(ResourceOwner.Stonehaven));
        Assert.Equal(nameof(SettlementRecoveryStatus.Defeated), defeated.Status);
        Assert.Equal(1, defeated.RecoverySecondsRemaining);
        Assert.Equal(0, await database.Settlements
            .Where(x => x.Id == LivingRealmsDbContext.StonehavenVillageId)
            .Select(x => x.Population)
            .SingleAsync());

        var rebuilding = Assert.Single(
            await recovery.AdvanceAsync(defeatedAt.AddMinutes(15)),
            x => x.Owner == nameof(ResourceOwner.Stonehaven));
        Assert.Equal(nameof(SettlementRecoveryStatus.Rebuilding), rebuilding.Status);
        Assert.Equal(WorldPopulationService.StartingStonehavenPopulation, await database.SettlementResidents
            .CountAsync(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                             x.Health > 0 &&
                             x.Status == ResidentStatus.Active));

        var settlement = await database.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
        settlement.Wood = 10_000;
        settlement.Stone = 10_000;
        var wallProject = await database.ConstructionProjects
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId);
        wallProject.CurrentLevel = 1;
        await database.SaveChangesAsync();

        await recovery.AdvanceAsync(defeatedAt.AddMinutes(16), worldHours: 1);
        var structuresAfterOneHour = await database.WorldStructures.AsNoTracking()
            .Where(x => x.Owner == ResourceOwner.Stonehaven)
            .ToArrayAsync();
        Assert.Contains(structuresAfterOneHour, x =>
            x.Kind is not WorldStructureKind.Wall and not WorldStructureKind.Gate &&
            x.Health > 0);
        Assert.All(
            structuresAfterOneHour.Where(x =>
                x.Kind is WorldStructureKind.Wall or WorldStructureKind.Gate),
            x => Assert.Equal(0, x.Health));

        var completed = Assert.Single(
            await recovery.AdvanceAsync(defeatedAt.AddHours(2), worldHours: 100),
            x => x.Owner == nameof(ResourceOwner.Stonehaven));
        Assert.Equal(nameof(SettlementRecoveryStatus.Healthy), completed.Status);
        Assert.Equal(completed.FunctionalStructuresTotal, completed.FunctionalStructuresRestored);
        Assert.Equal(completed.DefensesTotal, completed.DefensesRestored);
        Assert.Equal(completed.StructureMaximumHealth, completed.StructureHealth);
    }

    [Fact]
    public async Task DarkwoodReturnsWithSevenFoundersAndRebuildsCampBeforePalisade()
    {
        var defeatedAt = new DateTimeOffset(2026, 7, 27, 13, 0, 0, TimeSpan.Zero);
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var population = scope.ServiceProvider.GetRequiredService<WorldPopulationService>();
        var recovery = scope.ServiceProvider.GetRequiredService<SettlementRecoveryService>();
        await population.EnsureDarkwoodClanMembersAsync();

        var faction = await database.Factions
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        faction.DevelopmentStage = 3;
        var palisade = await database.ConstructionProjects
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId);
        palisade.CurrentLevel = 3;
        await database.SaveChangesAsync();

        await recovery.MarkDefeatedAsync(ResourceOwner.Darkwood, defeatedAt);
        await recovery.AdvanceAsync(defeatedAt.AddMinutes(15));
        database.ChangeTracker.Clear();

        faction = await database.Factions
            .Include(x => x.Resources)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId);
        Assert.Equal(WorldPopulationService.StartingDarkwoodPopulation, faction.Population);
        Assert.Equal(1, faction.DevelopmentStage);
        Assert.Equal(WorldPopulationService.StartingDarkwoodPopulation, await database.Creatures
            .CountAsync(x => x.FactionId == faction.Id &&
                             x.Status == CreatureStatus.Alive &&
                             x.Health > 0));
        Assert.Equal(
            CreatureStatus.Alive,
            (await database.Creatures.SingleAsync(x =>
                x.Id == LivingRealmsDbContext.GoblinChiefCreatureId)).Status);

        faction.Resources.Single(x => x.Kind == ResourceKind.Wood).Amount = 10_000;
        faction.Resources.Single(x => x.Kind == ResourceKind.Stone).Amount = 10_000;
        palisade = await database.ConstructionProjects
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId);
        palisade.CurrentLevel = 1;
        await database.SaveChangesAsync();

        await recovery.AdvanceAsync(defeatedAt.AddMinutes(16), worldHours: 1);
        var structuresAfterOneHour = await database.WorldStructures.AsNoTracking()
            .Where(x => x.Owner == ResourceOwner.Darkwood)
            .ToArrayAsync();
        Assert.Contains(structuresAfterOneHour, x =>
            x.Kind is not WorldStructureKind.Wall and not WorldStructureKind.Gate &&
            x.Health > 0);
        Assert.All(
            structuresAfterOneHour.Where(x => x.Kind == WorldStructureKind.Wall),
            x => Assert.Equal(0, x.Health));

        var completed = Assert.Single(
            await recovery.AdvanceAsync(defeatedAt.AddHours(1), worldHours: 24),
            x => x.Owner == nameof(ResourceOwner.Darkwood));
        Assert.Equal(nameof(SettlementRecoveryStatus.Healthy), completed.Status);
        Assert.Equal(completed.FunctionalStructuresTotal, completed.FunctionalStructuresRestored);
        Assert.Equal(completed.DefensesTotal, completed.DefensesRestored);
    }

    [Fact]
    public async Task LivingWorldResetClearsRecoveryTimersAndRestoresHealthyState()
    {
        using var scope = _factory.Services.CreateScope();
        var recovery = scope.ServiceProvider.GetRequiredService<SettlementRecoveryService>();
        var simulation = scope.ServiceProvider.GetRequiredService<WorldSimulationService>();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var now = DateTimeOffset.UtcNow;

        await recovery.MarkDefeatedAsync(ResourceOwner.Stonehaven, now);
        await recovery.MarkDefeatedAsync(ResourceOwner.Darkwood, now);
        await simulation.ResetForTestingAsync(now.AddMinutes(1));

        var states = await database.SettlementRecoveries.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, states.Length);
        Assert.All(states, state =>
        {
            Assert.Equal(SettlementRecoveryStatus.Healthy, state.Status);
            Assert.Null(state.DefeatedAt);
            Assert.Null(state.RecoveryEligibleAt);
            Assert.Null(state.RebuildingStartedAt);
            Assert.Null(state.CurrentStructureKey);
            Assert.Equal(0, state.RebuildCycles);
        });
    }
}
