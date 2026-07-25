using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class DevelopmentEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public DevelopmentEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PlayerCanGatherThenCompleteAnIndependentWallTier()
    {
        using var client = _factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"builder-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = Assert.IsType<AuthenticationResponse>(
            await registrationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);

        var state = Assert.IsType<DevelopmentStateResponse>(
            await client.GetFromJsonAsync<DevelopmentStateResponse>("/api/v1/development/state", JsonOptions));
        Assert.Equal(8, state.Nodes.Length);
        Assert.Equal(5, state.Projects.Length);
        Assert.Contains(state.Projects, x => x.Key == "stonehaven-curtain-wall" && x.CurrentLevel == 0);
        Assert.Contains(state.Projects, x =>
            x.Key == "stonehaven-lumber-yard" &&
            x.CurrentLevel == 0 &&
            x.Position.X == -22.0f &&
            x.Position.Z == -19.5f);
        Assert.Contains(state.Projects, x =>
            x.Key == "stonehaven-quarry-works" &&
            x.Position.X == 88.0f &&
            x.Position.Z == -91.0f);
        Assert.Contains(state.Nodes, x =>
            x.Key == "irondeep-ore-vein" &&
            x.Kind == "Iron" &&
            x.Position.X == 121.0f &&
            x.Position.Z == -103.0f);

        var oak = Assert.Single(state.Nodes, x => x.Key == "stonehaven-oak-west");
        var harvestResponse = await client.PostAsJsonAsync(
            "/api/v1/development/harvest",
            new HarvestRequest(oak.Id, oak.Position));
        Assert.True(
            harvestResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK, received {harvestResponse.StatusCode}: {await harvestResponse.Content.ReadAsStringAsync()}");
        var harvested = Assert.IsType<DevelopmentActionResponse>(
            await harvestResponse.Content.ReadFromJsonAsync<DevelopmentActionResponse>(JsonOptions));
        Assert.Equal(state.SettlementStores.Wood, harvested.State.SettlementStores.Wood);
        var gatheredInventory = Assert.IsType<InventoryResponse>(
            await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions));
        var timber = Assert.Single(gatheredInventory.Items, x => x.Key == "raw-timber");
        Assert.Equal(oak.YieldPerHarvest, timber.Quantity);
        Assert.True(gatheredInventory.UsedCapacity > 0);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var wall = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId);
            wall.WoodContributed = wall.WoodRequired - 10;
            wall.StoneContributed = wall.StoneRequired - 10;
            var timberEntry = await database.CharacterInventory
                .SingleAsync(x => x.CharacterId == alden.Id && x.ItemId == LivingRealmsDbContext.TimberItemId);
            timberEntry.Quantity = 10;
            database.CharacterInventory.Add(new CharacterInventory
            {
                CharacterId = alden.Id,
                ItemId = LivingRealmsDbContext.RoughStoneItemId,
                Quantity = 10
            });
            await database.SaveChangesAsync();
        }

        var wallProject = Assert.Single(harvested.State.Projects, x => x.Key == "stonehaven-curtain-wall");
        var contributeResponse = await client.PostAsJsonAsync(
            "/api/v1/development/contribute",
            new ContributeRequest(wallProject.Id, wallProject.Position));
        Assert.Equal(HttpStatusCode.OK, contributeResponse.StatusCode);
        var contribution = Assert.IsType<DevelopmentActionResponse>(
            await contributeResponse.Content.ReadFromJsonAsync<DevelopmentActionResponse>(JsonOptions));
        var upgradedWall = Assert.Single(contribution.State.Projects, x => x.Key == "stonehaven-curtain-wall");
        Assert.Equal(1, upgradedWall.CurrentLevel);
        Assert.Equal(0, upgradedWall.WoodContributed);
        Assert.Equal(0, upgradedWall.StoneContributed);

        using var verificationScope = _factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var settlement = await verification.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId);
        Assert.Equal(77, settlement.DefenseRating);
        Assert.Equal(46, settlement.GuardStrength);
        Assert.Equal(1220, settlement.StructuralIntegrity);
        Assert.Equal(0, await verification.ConstructionProjects
            .Where(x => x.Id == LivingRealmsDbContext.StonehavenLumberYardProjectId)
            .Select(x => x.CurrentLevel)
            .SingleAsync());
    }

    [Fact]
    public async Task NaturalGatheringStopsAtThePersistentCarryLimit()
    {
        using var client = _factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"packer-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = Assert.IsType<AuthenticationResponse>(
            await registrationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);
        _ = Assert.IsType<InventoryResponse>(
            await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions));

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var character = await database.Characters.SingleAsync(x => x.Id == alden.Id);
            character.CarryCapacity = 14;
            await database.SaveChangesAsync();
        }
        var position = new PositionResponse(20, 0.08f, 20);
        var full = await client.PostAsJsonAsync(
            "/api/v1/development/harvest-natural",
            new NaturalHarvestRequest("Wood", position, position));
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var character = await database.Characters.SingleAsync(x => x.Id == alden.Id);
            character.CarryCapacity = 16;
            character.LastGatherAt = null;
            await database.SaveChangesAsync();
        }
        var partial = await client.PostAsJsonAsync(
            "/api/v1/development/harvest-natural",
            new NaturalHarvestRequest("Wood", position, position));
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var inventory = Assert.IsType<InventoryResponse>(
            await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions));
        Assert.Equal(16, inventory.UsedCapacity);
        Assert.Equal(2, Assert.Single(inventory.Items, x => x.Key == "raw-timber").Quantity);
    }

    [Fact]
    public async Task PlayerCanMinePersistentIronAtIrondeep()
    {
        using var client = _factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"miner-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        var registration = Assert.IsType<AuthenticationResponse>(
            await registrationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);

        var state = Assert.IsType<DevelopmentStateResponse>(
            await client.GetFromJsonAsync<DevelopmentStateResponse>("/api/v1/development/state", JsonOptions));
        var iron = Assert.Single(state.Nodes, x => x.Key == "irondeep-ore-vein");
        var response = await client.PostAsJsonAsync(
            "/api/v1/development/harvest",
            new HarvestRequest(iron.Id, iron.Position));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inventory = Assert.IsType<InventoryResponse>(
            await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions));
        var ore = Assert.Single(inventory.Items, x => x.Key == "raw-iron-ore");
        Assert.Equal(iron.YieldPerHarvest, ore.Quantity);
    }

    [Fact]
    public async Task NessaBuildsTheLumberYardInsteadOfReportingWorkAtTheWall()
    {
        using var client = _factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"lumber-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        var registration = Assert.IsType<AuthenticationResponse>(
            await registrationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = Assert.Single(registration.Characters, x => x.Name == "Alden");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null)).StatusCode);

        DevelopmentStateResponse state;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var lumberYard = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenLumberYardProjectId);
            var wall = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId);
            lumberYard.CurrentLevel = 0;
            lumberYard.CompletedAt = null;
            lumberYard.WoodContributed = 0;
            lumberYard.StoneContributed = 0;
            wall.WoodContributed = 0;
            wall.StoneContributed = 0;
            var oak = await database.WorldResourceNodes
                .SingleAsync(x => x.Key == "stonehaven-oak-west");
            oak.Remaining = oak.Capacity;
            oak.RespawnAt = null;
            var previousNessaContributions = await database.ResourceContributions
                .Where(x => x.ContributorName == "Nessa" && x.Source == "NPC")
                .ToListAsync();
            database.ResourceContributions.RemoveRange(previousNessaContributions);
            await database.SaveChangesAsync();
            state = Assert.IsType<DevelopmentStateResponse>(
                await client.GetFromJsonAsync<DevelopmentStateResponse>("/api/v1/development/state", JsonOptions));
        }

        var woodNode = Assert.Single(state.Nodes, x => x.Key == "stonehaven-oak-west");
        var response = await client.PostAsJsonAsync(
            "/api/v1/development/npc-work",
            new NpcWorkRequest("nessa", woodNode.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<DevelopmentActionResponse>(
            await response.Content.ReadFromJsonAsync<DevelopmentActionResponse>(JsonOptions));
        Assert.True(Assert.Single(result.State.Projects, x => x.Key == "stonehaven-lumber-yard").WoodContributed > 0);
        Assert.Equal(0, Assert.Single(result.State.Projects, x => x.Key == "stonehaven-curtain-wall").WoodContributed);
        Assert.Contains(result.State.RecentContributions, x =>
            x.ContributorName == "Nessa" && x.Kind == "Wood" && x.Source == "NPC");
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record AuthenticationResponse(string Token, CharacterResponse[] Characters);
    private sealed record CharacterResponse(Guid Id, string Name);
    private sealed record PositionResponse(float X, float Y, float Z);
    private sealed record HarvestRequest(Guid NodeId, PositionResponse PlayerPosition);
    private sealed record NaturalHarvestRequest(
        string Kind, PositionResponse ResourcePosition, PositionResponse PlayerPosition);
    private sealed record ContributeRequest(Guid ProjectId, PositionResponse PlayerPosition);
    private sealed record NpcWorkRequest(string WorkerKey, Guid NodeId);
    private sealed record DevelopmentActionResponse(DevelopmentStateResponse State, string Message);
    private sealed record DevelopmentStateResponse(
        ResourceNodeResponse[] Nodes,
        ConstructionProjectResponse[] Projects,
        ContributionResponse[] RecentContributions,
        SettlementStoresResponse SettlementStores);
    private sealed record ResourceNodeResponse(
        Guid Id, string Key, string Name, string Kind, string Owner, PositionResponse Position,
        int Remaining, int Capacity, int YieldPerHarvest, DateTimeOffset? RespawnAt);
    private sealed record ConstructionProjectResponse(
        Guid Id, string Key, string Name, string Owner,
        int WoodRequired, int StoneRequired, int WoodContributed, int StoneContributed,
        int CurrentLevel, int MaximumLevel, float Progress, string Stage,
        PositionResponse Position, DateTimeOffset? CompletedAt);
    private sealed record ContributionResponse(
        string ContributorName, string Kind, int Amount, string Source, DateTimeOffset OccurredAt);
    private sealed record SettlementStoresResponse(int Wood, int Stone);
    private sealed record InventoryResponse(
        Guid CharacterId, int Attack, int Defense, int Gold, int UsedCapacity, int CarryCapacity,
        InventoryItemResponse[] Items);
    private sealed record InventoryItemResponse(Guid Id, string Key, int Quantity);
}
