using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class DestructibleStructureTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private readonly PhaseTwoWebApplicationFactory _factory;

    public DestructibleStructureTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        database.Database.EnsureDeleted();
        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task RegistryReportsBuiltAssetsAndUnbuiltConstructionSeparately()
    {
        using var scope = _factory.Services.CreateScope();
        var structures = scope.ServiceProvider.GetRequiredService<WorldStructureService>();

        var state = await structures.GetStatesAsync();

        Assert.Equal(34, state.Count);
        Assert.Contains(state, x =>
            x.Key == "stonehaven-gate" &&
            x.Status == "Healthy" &&
            x.IsBuilt &&
            x.BlocksMovement &&
            x.Health == x.MaximumHealth);
        Assert.Contains(state, x =>
            x.Key == "stonehaven-wall-west" &&
            x.Status == "Unbuilt" &&
            !x.IsBuilt &&
            !x.BlocksMovement &&
            x.Health == 0);
        Assert.Contains(state, x =>
            x.Key == "darkwood-hide-tents" &&
            x.Status == "Healthy" &&
            x.IsBuilt);
    }

    [Fact]
    public async Task DestroyedWallPersistsAsABreachAndStopsBlockingMovement()
    {
        var damagedAt = new DateTimeOffset(2026, 7, 27, 1, 0, 0, TimeSpan.Zero);
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var wallProject = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId);
            wallProject.CurrentLevel = 1;
            await database.SaveChangesAsync();

            var structures = scope.ServiceProvider.GetRequiredService<WorldStructureService>();
            var result = await structures.DamageStructureAsync(
                "stonehaven-wall-west",
                5000,
                damagedAt);
            Assert.NotNull(result);
            Assert.True(result.Destroyed);
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<WorldStructureService>();
        var wall = Assert.Single(
            await verification.GetStatesAsync(ResourceOwner.Stonehaven),
            x => x.Key == "stonehaven-wall-west");
        Assert.Equal(0, wall.Health);
        Assert.Equal("Destroyed", wall.Status);
        Assert.False(wall.BlocksMovement);
        Assert.Equal(damagedAt, wall.DestroyedAt);
    }

    [Fact]
    public async Task PlaytestResetRestoresEveryStructureToFullPersistentHealth()
    {
        using (var damageScope = _factory.Services.CreateScope())
        {
            var structures = damageScope.ServiceProvider.GetRequiredService<WorldStructureService>();
            await structures.DamageStructureAsync(
                "stonehaven-gate",
                700,
                DateTimeOffset.UtcNow);
        }

        using (var resetScope = _factory.Services.CreateScope())
        {
            var simulation = resetScope.ServiceProvider.GetRequiredService<WorldSimulationService>();
            await simulation.ResetForTestingAsync(DateTimeOffset.UtcNow);
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        Assert.All(await verification.WorldStructures.ToArrayAsync(), structure =>
        {
            Assert.Equal(structure.MaximumHealth, structure.Health);
            Assert.Null(structure.LastDamagedAt);
            Assert.Null(structure.DestroyedAt);
        });
    }
}
