using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingRealms.Api.Tests;

public sealed class PhaseFiveEndpointTests : IClassFixture<PhaseTwoWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PhaseTwoWebApplicationFactory _factory;

    public PhaseFiveEndpointTests(PhaseTwoWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task InventoryCreatesEquippedStarterLoadoutAndArchetypeSkills()
    {
        using var client = _factory.CreateClient();
        _ = await RegisterAndSelectAldenAsync(client);

        var inventory = await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions);
        Assert.NotNull(inventory);
        Assert.Equal(34, inventory.Attack);
        Assert.Equal(11, inventory.Defense);
        Assert.Equal(0, inventory.Gold);
        Assert.Equal(14, inventory.UsedCapacity);
        Assert.Equal(80, inventory.CarryCapacity);
        Assert.Equal(2, inventory.Items.Count);
        Assert.All(inventory.Items, item => Assert.True(item.IsEquipped));
        Assert.Contains(inventory.Items, item => item.Key == "stonehaven-training-blade");
        Assert.Contains(inventory.Items, item => item.Key == "stonehaven-leather-guard");
        Assert.All(inventory.Items, item => Assert.True(item.UnitWeight > 0));

        var skills = await client.GetFromJsonAsync<SkillResponse[]>("/api/v1/skills", JsonOptions);
        Assert.NotNull(skills);
        Assert.Equal(["second-wind", "shield-bash"], skills.Select(x => x.Key).Order().ToArray());
        Assert.Contains(skills, skill => skill.Hotkey == "Q" && skill.IsOffensive);
        Assert.Contains(skills, skill => skill.Hotkey == "E" && !skill.IsOffensive);
    }

    [Fact]
    public async Task StonehavenBuyersPurchaseUnequippedItemsAndPayPersistentGold()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Guid tailEntryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            tailEntryId = Guid.NewGuid();
            database.CharacterInventory.Add(new CharacterInventory
            {
                Id = tailEntryId,
                CharacterId = alden.Id,
                ItemId = LivingRealmsDbContext.RatTailItemId,
                Quantity = 1
            });
            await database.SaveChangesAsync();
        }

        var saleResponse = await client.PostAsJsonAsync(
            $"/api/v1/inventory/{tailEntryId:D}/sell",
            new SellRequest(new Position(12, 0.08f, -23.6f)));
        Assert.Equal(HttpStatusCode.OK, saleResponse.StatusCode);
        var sale = await saleResponse.Content.ReadFromJsonAsync<ItemSaleResponse>(JsonOptions);
        Assert.NotNull(sale);
        Assert.Equal("Oren the Storekeeper", sale.BuyerName);
        Assert.Equal(1, sale.GoldReceived);
        Assert.Equal(1, sale.Inventory.Gold);
        Assert.DoesNotContain(sale.Inventory.Items, x => x.Id == tailEntryId);
    }

    [Fact]
    public async Task CreatureDefeatAwardsPersistentLootAndConsumableRestoresHealth()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Creature rat;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            rat = await database.Creatures.Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId);
            rat.Status = CreatureStatus.Alive;
            rat.Health = 1;
            rat.RespawnAt = null;
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.LastAttackAt = null;
            await database.SaveChangesAsync();
        }

        var combatResponse = await client.PostAsJsonAsync(
            "/api/v1/combat/player-attack",
            new CombatRequest(rat.Id, new Position(rat.PositionX, rat.PositionY, rat.PositionZ),
                new Position(rat.PositionX, rat.PositionY, rat.PositionZ)));
        Assert.Equal(HttpStatusCode.OK, combatResponse.StatusCode);
        var combat = await combatResponse.Content.ReadFromJsonAsync<CombatResponse>(JsonOptions);
        Assert.NotNull(combat);
        Assert.True(combat.CreatureDefeated);
        Assert.Contains(combat.Loot, x => x.Key == "forest-rat-tail");
        Assert.Contains(combat.Loot, x => x.Key == "field-tonic");

        var inventory = await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions);
        Assert.NotNull(inventory);
        var tonic = Assert.Single(inventory.Items, x => x.Key == "field-tonic");

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.Health = 50;
            await database.SaveChangesAsync();
        }

        var used = await client.PostAsync($"/api/v1/inventory/{tonic.Id:D}/use", null);
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        var result = await used.Content.ReadFromJsonAsync<ItemUseResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(85, result.Character.Health);
        Assert.DoesNotContain(result.Inventory.Items, x => x.Key == "field-tonic");
    }

    [Fact]
    public async Task EquipmentChangesDefenseAndSkillsDealDamageHealAndEnforceCooldowns()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        Creature rat;
        Guid peltEntryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            rat = await database.Creatures.Include(x => x.Species)
                .FirstAsync(x => x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId);
            rat.Status = CreatureStatus.Alive;
            rat.Health = rat.MaximumHealth;
            rat.RespawnAt = null;
            peltEntryId = Guid.NewGuid();
            database.CharacterInventory.Add(new CharacterInventory
            {
                Id = peltEntryId,
                CharacterId = alden.Id,
                ItemId = LivingRealmsDbContext.WolfPeltItemId,
                Quantity = 1
            });
            var character = await database.Characters.FindAsync(alden.Id);
            Assert.NotNull(character);
            character.Health = 50;
            await database.SaveChangesAsync();
        }

        _ = await client.GetAsync("/api/v1/inventory");
        var equippedResponse = await client.PostAsync($"/api/v1/inventory/{peltEntryId:D}/equip", null);
        Assert.Equal(HttpStatusCode.OK, equippedResponse.StatusCode);
        var equipped = await equippedResponse.Content.ReadFromJsonAsync<InventoryResponse>(JsonOptions);
        Assert.NotNull(equipped);
        Assert.Equal(13, equipped.Defense);
        Assert.True(equipped.Items.Single(x => x.Id == peltEntryId).IsEquipped);
        Assert.False(equipped.Items.Single(x => x.Key == "stonehaven-leather-guard").IsEquipped);

        var skillPayload = new SkillRequest(
            "shield-bash",
            rat.Id,
            new Position(rat.PositionX, rat.PositionY, rat.PositionZ),
            new Position(rat.PositionX, rat.PositionY, rat.PositionZ));
        var skillResponse = await client.PostAsJsonAsync("/api/v1/combat/player-skill", skillPayload);
        Assert.Equal(HttpStatusCode.OK, skillResponse.StatusCode);
        var skill = await skillResponse.Content.ReadFromJsonAsync<SkillUseResponse>(JsonOptions);
        Assert.NotNull(skill);
        Assert.True(skill.Damage > 0);
        Assert.Equal("shield-bash", skill.SkillKey);

        var cooldown = await client.PostAsJsonAsync("/api/v1/combat/player-skill", skillPayload);
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);

        var healing = await client.PostAsJsonAsync(
            "/api/v1/combat/player-skill",
            new SkillRequest("second-wind", null, new Position(0, 0.08f, 8), null));
        Assert.Equal(HttpStatusCode.OK, healing.StatusCode);
        var healed = await healing.Content.ReadFromJsonAsync<SkillUseResponse>(JsonOptions);
        Assert.NotNull(healed);
        Assert.Equal(32, healed.Healed);
        Assert.Equal(82, healed.Character.Health);
    }

    [Fact]
    public async Task InventoryRepairsDuplicateSlotsAndDoesNotRecreateReplacedStarterGear()
    {
        using var client = _factory.CreateClient();
        var registration = await RegisterAndSelectAldenAsync(client);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        _ = await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            database.CharacterInventory.AddRange(
                new CharacterInventory
                {
                    Id = Guid.NewGuid(),
                    CharacterId = alden.Id,
                    ItemId = LivingRealmsDbContext.GoblinBladeItemId,
                    Quantity = 1,
                    IsEquipped = true
                },
                new CharacterInventory
                {
                    Id = Guid.NewGuid(),
                    CharacterId = alden.Id,
                    ItemId = LivingRealmsDbContext.WolfPeltItemId,
                    Quantity = 1,
                    IsEquipped = true
                });
            await database.SaveChangesAsync();
        }

        var repaired = await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions);
        Assert.NotNull(repaired);
        Assert.Equal(2, repaired.Items.Count(x => x.IsEquipped));
        Assert.True(repaired.Items.Single(x => x.Key == "goblin-raider-blade").IsEquipped);
        Assert.True(repaired.Items.Single(x => x.Key == "prairie-wolf-pelt").IsEquipped);
        Assert.False(repaired.Items.Single(x => x.Key == "stonehaven-training-blade").IsEquipped);
        Assert.False(repaired.Items.Single(x => x.Key == "stonehaven-leather-guard").IsEquipped);
        Assert.Equal(38, repaired.Attack);
        Assert.Equal(13, repaired.Defense);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<LivingRealmsDbContext>();
            var starterEntries = await database.CharacterInventory
                .Where(x => x.CharacterId == alden.Id &&
                            (x.ItemId == LivingRealmsDbContext.TrainingBladeItemId ||
                             x.ItemId == LivingRealmsDbContext.LeatherGuardItemId))
                .ToListAsync();
            database.CharacterInventory.RemoveRange(starterEntries);
            await database.SaveChangesAsync();
        }

        var withoutRespawnedStarters = await client.GetFromJsonAsync<InventoryResponse>("/api/v1/inventory", JsonOptions);
        Assert.NotNull(withoutRespawnedStarters);
        Assert.DoesNotContain(withoutRespawnedStarters.Items, x => x.Key == "stonehaven-training-blade");
        Assert.DoesNotContain(withoutRespawnedStarters.Items, x => x.Key == "stonehaven-leather-guard");
        Assert.Equal(2, withoutRespawnedStarters.Items.Count(x => x.IsEquipped));
    }

    private static async Task<AuthenticationResponse> RegisterAndSelectAldenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounts/register",
            new Credentials($"phase5-{Guid.NewGuid():N}@living-realms.test", "Stonehaven42!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(JsonOptions);
        Assert.NotNull(registration);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.Token);
        var alden = registration.Characters.Single(x => x.Name == "Alden");
        var selected = await client.PostAsync($"/api/v1/characters/{alden.Id:D}/select", null);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        return registration;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record Position(float X, float Y, float Z);
    private sealed record CombatRequest(Guid CreatureId, Position PlayerPosition, Position CreaturePosition);
    private sealed record SkillRequest(string SkillKey, Guid? CreatureId, Position PlayerPosition, Position? CreaturePosition);
    private sealed record SellRequest(Position PlayerPosition);
    private sealed record AuthenticationResponse(string Token, DateTimeOffset ExpiresAt, AccountResponse Account, CharacterResponse[] Characters);
    private sealed record AccountResponse(Guid Id, string Email);
    private sealed record CharacterResponse(Guid Id, string Name, string Archetype, int Level, long Experience, int Health,
        int MaximumHealth, string Region, Position Position, DateTimeOffset UpdatedAt);
    private sealed record InventoryResponse(
        Guid CharacterId, int Attack, int Defense, int Gold, int UsedCapacity, int CarryCapacity,
        List<InventoryItemResponse> Items);
    private sealed record InventoryItemResponse(Guid Id, Guid ItemId, string Key, string Name, string Kind, string Rarity,
        string? EquipmentSlot, int AttackBonus, int DefenseBonus, int HealingAmount,
        int UnitWeight, int TotalWeight, string BuyerName, int Quantity, bool IsEquipped);
    private sealed record SkillResponse(string Key, string Name, string Hotkey, double CooldownSeconds, bool IsOffensive);
    private sealed record LootResponse(Guid ItemId, string Key, string Name, string Rarity, int Quantity);
    private sealed record CreatureResponse(Guid Id, string Name, int Health, int MaximumHealth, string Status);
    private sealed record CombatResponse(CharacterResponse Character, CreatureResponse Creature, int Damage,
        int ExperienceGained, bool LeveledUp, bool CreatureDefeated, bool CharacterKnockedOut, List<LootResponse> Loot, string Message);
    private sealed record ItemUseResponse(CharacterResponse Character, InventoryResponse Inventory, string Message);
    private sealed record ItemSaleResponse(
        InventoryResponse Inventory, int GoldReceived, string BuyerName, string Message);
    private sealed record SkillUseResponse(CharacterResponse Character, CreatureResponse? Creature, string SkillKey,
        int Damage, int Healed, int ExperienceGained, bool LeveledUp, bool CreatureDefeated, List<LootResponse> Loot, string Message);
}
