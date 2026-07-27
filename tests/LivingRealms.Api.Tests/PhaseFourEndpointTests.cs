using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseFourEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseFourEndpointTests(PhaseTwoWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StonehavenCreatureRosterContainsAllFourPhaseFourSpecies()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);

        var creatures = await client.GetFromJsonAsync<CreatureResponse[]>(
            "/api/v1/regions/stonehaven-valley/creatures",
            JsonOptions);

        Assert.NotNull(creatures);
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var factionPopulation = await database.Factions
                .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
                .Select(x => x.Population)
                .SingleAsync();
            Assert.Equal(
                factionPopulation,
                creatures.Count(x => x.Role is "Chief" or "Raider" or "Clan Raider" or "Clan Hunter" or "Woodcutter" or "Stone Gatherer" or "Iron Miner" or "Camp Guard" or "Scout"));
        }
        Assert.Equal(
            ["forest-rat", "goblin-chief", "goblin-raider", "prairie-wolf"],
            creatures.Select(x => x.SpeciesKey).Distinct().Order().ToArray());
        var testCreatures = creatures
            .Where(x => x.SpeciesKey is "forest-rat" or "prairie-wolf")
            .Where(x => x.Role != "Wildlife")
            .ToArray();
        Assert.Equal(5, testCreatures.Length);
        Assert.All(testCreatures, creature =>
        {
            Assert.InRange(creature.Position.X, 48.0f, 144.0f);
            Assert.InRange(creature.Position.Z, 48.0f, 144.0f);
        });
        Assert.Equal(
            10,
            creatures.Count(x =>
                x.Role == "Wildlife" &&
                x.SpeciesKey is "forest-rat" or "prairie-wolf"));
        Assert.Single(creatures, x => x.IsBoss && x.SpeciesKey == "goblin-chief");
        Assert.Equal("Alden", registration.Characters.Single(x => x.Name == "Alden").Name);
    }

    [Fact]
    public async Task PlayerAttackDefeatsCreatureAwardsExperienceAndPersistsDeath()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Creature rat;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            rat = await database.Creatures
                .Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId);
            rat.Status = CreatureStatus.Alive;
            rat.Health = 1;
            rat.RespawnAt = null;
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.Experience = 90;
            character.LastAttackAt = null;
            await database.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/combat/player-attack",
            new CombatRequest(
                rat.Id,
                new Position(rat.PositionX, rat.PositionY, rat.PositionZ),
                new Position(rat.PositionX, rat.PositionY, rat.PositionZ)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var combat = await response.Content.ReadFromJsonAsync<CombatResponse>(JsonOptions);
        Assert.NotNull(combat);
        Assert.True(combat.CreatureDefeated);
        Assert.True(combat.LeveledUp);
        Assert.Equal(2, combat.Character.Level);
        Assert.Equal(15, combat.Character.Experience);
        Assert.Equal("Dead", combat.Creature.Status);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var persisted = await verifyDatabase.Creatures.FindAsync(rat.Id);
        Assert.NotNull(persisted);
        Assert.Equal(CreatureStatus.Dead, persisted.Status);
        Assert.NotNull(persisted.RespawnAt);
    }

    [Theory]
    [InlineData("Alden")]
    [InlineData("Elara")]
    public async Task BothPlayerCharactersCanAttackAValidTarget(string characterName)
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectCharacterAsync(client, characterName);
        var character = registration.Characters.Single(x => x.Name == characterName);
        Creature rat;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            rat = await database.Creatures
                .Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId);
            rat.Status = CreatureStatus.Alive;
            rat.Health = rat.MaximumHealth;
            rat.RespawnAt = null;
            var persistedCharacter = await database.Characters.FindAsync(character.Id);
            Assert.NotNull(persistedCharacter);
            persistedCharacter.LastAttackAt = null;
            await database.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/combat/player-attack",
            new CombatRequest(
                rat.Id,
                new Position(rat.PositionX, rat.PositionY, rat.PositionZ),
                new Position(rat.PositionX, rat.PositionY, rat.PositionZ)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var combat = await response.Content.ReadFromJsonAsync<CombatResponse>(JsonOptions);
        Assert.NotNull(combat);
        Assert.Equal(characterName, combat.Character.Name);
        Assert.True(combat.Damage > 0);
        Assert.True(combat.Creature.Health < combat.Creature.MaximumHealth);
    }

    [Fact]
    public async Task DefeatedCreatureRespawnsWhenItsPersistentTimerExpires()
    {
        using var client = _factory.CreateClient();
        _ = await RegisterAndSelectAldenAsync(client);
        Guid ratId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var rat = await database.Creatures
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId);
            ratId = rat.Id;
            rat.Status = CreatureStatus.Dead;
            rat.Health = 0;
            rat.RespawnAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await database.SaveChangesAsync();
        }

        var creatures = await client.GetFromJsonAsync<CreatureResponse[]>(
            "/api/v1/regions/stonehaven-valley/creatures",
            JsonOptions);
        var respawned = Assert.Single(Assert.IsType<CreatureResponse[]>(creatures), x => x.Id == ratId);
        Assert.Equal("Alive", respawned.Status);
        Assert.Equal(respawned.MaximumHealth, respawned.Health);
        Assert.Null(respawned.RespawnAt);
    }

    [Fact]
    public async Task CreatureAttackDamagesCharacterAndOutOfRangePlayerAttackIsRejected()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Creature wolf;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            wolf = await database.Creatures
                .Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.PrairieWolfSpeciesId);
            wolf.Status = CreatureStatus.Alive;
            wolf.Health = wolf.MaximumHealth;
            wolf.RespawnAt = null;
            wolf.LastAttackAt = null;
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.Health = character.MaximumHealth;
            character.LastAttackAt = null;
            await database.SaveChangesAsync();
        }

        var creatureAttack = await client.PostAsJsonAsync(
            "/api/v1/combat/creature-attack",
            new CombatRequest(
                wolf.Id,
                new Position(wolf.PositionX, wolf.PositionY, wolf.PositionZ),
                new Position(wolf.PositionX, wolf.PositionY, wolf.PositionZ)));
        Assert.Equal(HttpStatusCode.OK, creatureAttack.StatusCode);
        var combat = await creatureAttack.Content.ReadFromJsonAsync<CombatResponse>(JsonOptions);
        Assert.NotNull(combat);
        Assert.True(combat.Damage > 0);
        Assert.True(combat.Character.Health < combat.Character.MaximumHealth);

        var outOfRange = await client.PostAsJsonAsync(
            "/api/v1/combat/player-attack",
            new CombatRequest(
                wolf.Id,
                new Position(49, 0.08f, 49),
                new Position(wolf.PositionX, wolf.PositionY, wolf.PositionZ)));
        Assert.Equal(HttpStatusCode.Conflict, outOfRange.StatusCode);
    }

    [Fact]
    public async Task KnockoutReturnsAttackerHomeAndRejectsQueuedRepeatAttack()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Creature chief;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            chief = await database.Creatures
                .Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.GoblinChiefSpeciesId);
            chief.Status = CreatureStatus.Alive;
            chief.Health = chief.MaximumHealth;
            chief.RespawnAt = null;
            chief.LastAttackAt = null;
            chief.PositionX = chief.SpawnX + 4;
            chief.PositionZ = chief.SpawnZ + 4;
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.Health = 1;
            character.LastAttackAt = null;
            await database.SaveChangesAsync();
        }

        var staleFightPosition = new Position(chief.PositionX, chief.PositionY, chief.PositionZ);
        var knockoutResponse = await client.PostAsJsonAsync(
            "/api/v1/combat/creature-attack",
            new CombatRequest(chief.Id, staleFightPosition, staleFightPosition));
        Assert.Equal(HttpStatusCode.OK, knockoutResponse.StatusCode);
        var knockout = await knockoutResponse.Content.ReadFromJsonAsync<CombatResponse>(JsonOptions);
        Assert.NotNull(knockout);
        Assert.True(knockout.CharacterKnockedOut);
        Assert.Equal(knockout.Character.MaximumHealth, knockout.Character.Health);
        Assert.Equal(-2.0f, knockout.Character.Position.X);
        Assert.Equal(8.0f, knockout.Character.Position.Z);
        Assert.Equal(chief.SpawnX, knockout.Creature.Position.X);
        Assert.Equal(chief.SpawnZ, knockout.Creature.Position.Z);

        var queuedRepeat = await client.PostAsJsonAsync(
            "/api/v1/combat/creature-attack",
            new CombatRequest(chief.Id, staleFightPosition, staleFightPosition));
        Assert.Equal(HttpStatusCode.Conflict, queuedRepeat.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
        var persistedCharacter = await verifyDatabase.Characters.FindAsync(alden.Id);
        Assert.NotNull(persistedCharacter);
        Assert.Equal(persistedCharacter.MaximumHealth, persistedCharacter.Health);
        Assert.Equal(-2.0f, persistedCharacter.PositionX);
        Assert.Equal(8.0f, persistedCharacter.PositionZ);
    }

    private static async Task<AuthenticationResponse> RegisterAndSelectAldenAsync(HttpClient client)
    {
        return await RegisterAndSelectCharacterAsync(client, "Alden");
    }

    private static async Task<AuthenticationResponse> RegisterAndSelectCharacterAsync(
        HttpClient client,
        string characterName)
    {
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"phase4-{Guid.NewGuid():N}@living-realms.test", TestPassword));
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        Assert.NotNull(registration);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var character = registration.Characters.Single(x => x.Name == characterName);
        var selected = await client.PostAsync($"/api/v1/characters/{character.Id:D}/select", null);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        return registration;
    }

    private const string TestPassword = "Stonehaven42!";

    private sealed record Credentials(string Email, string Password);
    private sealed record Position(float X, float Y, float Z);
    private sealed record CombatRequest(Guid CreatureId, Position PlayerPosition, Position CreaturePosition);
    private sealed record AuthenticationResponse(
        string Token,
        DateTimeOffset ExpiresAt,
        AccountResponse Account,
        CharacterResponse[] Characters);
    private sealed record AccountResponse(Guid Id, string Email);
    private sealed record CharacterResponse(
        Guid Id,
        string Name,
        string Archetype,
        int Level,
        long Experience,
        int Health,
        int MaximumHealth,
        string Region,
        Position Position,
        DateTimeOffset UpdatedAt);
    private sealed record CreatureResponse(
        Guid Id,
        string SpeciesKey,
        string SpeciesName,
        string Name,
        string? Title,
        string? Role,
        int Level,
        int Health,
        int MaximumHealth,
        int Attack,
        int Defense,
        float MovementSpeed,
        float DetectionRadius,
        float AttackRange,
        int Aggression,
        string Status,
        Position Position,
        DateTimeOffset? RespawnAt,
        bool IsBoss);
    private sealed record CombatResponse(
        CharacterResponse Character,
        CreatureResponse Creature,
        int Damage,
        int ExperienceGained,
        bool LeveledUp,
        bool CreatureDefeated,
        bool CharacterKnockedOut,
        string Message);
}
