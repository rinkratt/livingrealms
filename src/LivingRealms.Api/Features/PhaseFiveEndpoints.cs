using System.Security.Claims;
using LivingRealms.Api.Logging;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class PhaseFiveEndpoints
{
    private const float MaximumWorldCoordinate = 142.0f;
    private const float MaximumWorldHeight = 20.0f;

    public static IEndpointRouteBuilder MapPhaseFiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var gameplay = endpoints.MapGroup("/api/v1").RequireAuthorization();
        gameplay.MapGet("/inventory", GetInventoryAsync);
        gameplay.MapPost("/inventory/{entryId:guid}/equip", EquipItemAsync).RequireRateLimiting("gameplay");
        gameplay.MapPost("/inventory/{entryId:guid}/unequip", UnequipItemAsync).RequireRateLimiting("gameplay");
        gameplay.MapPost("/inventory/{entryId:guid}/use", UseItemAsync).RequireRateLimiting("gameplay");
        gameplay.MapPost("/inventory/{entryId:guid}/sell", SellItemAsync).RequireRateLimiting("gameplay");
        gameplay.MapGet("/skills", GetSkillsAsync);
        gameplay.MapPost("/combat/player-skill", UseSkillAsync).RequireRateLimiting("gameplay");
        return endpoints;
    }

    private static async Task<IResult> GetInventoryAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before opening inventory."));
        }

        await EnsurePhaseFiveLoadoutAsync(character, database, context.RequestAborted);
        return Results.Ok(await BuildInventoryResponseAsync(character, database, context.RequestAborted));
    }

    private static async Task<IResult> EquipItemAsync(
        Guid entryId,
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before equipping items."));
        }

        await EnsurePhaseFiveLoadoutAsync(character, database, context.RequestAborted);
        var entry = await database.CharacterInventory
            .Include(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == entryId && x.CharacterId == character.Id, context.RequestAborted);
        if (entry is null)
        {
            return Results.NotFound(new ErrorResponse("Inventory item not found."));
        }

        if (entry.Item.EquipmentSlot is null)
        {
            return Results.Conflict(new ErrorResponse("That item cannot be equipped."));
        }

        if (entry.Item.RequiredArchetype is not null && entry.Item.RequiredArchetype != character.Archetype)
        {
            return Results.Conflict(new ErrorResponse($"{entry.Item.Name} cannot be equipped by {character.Name}."));
        }

        var slot = entry.Item.EquipmentSlot.Value;
        var equippedInSlot = await database.CharacterInventory
            .Include(x => x.Item)
            .Where(x => x.CharacterId == character.Id && x.IsEquipped && x.Item.EquipmentSlot == slot)
            .ToListAsync(context.RequestAborted);
        foreach (var equipped in equippedInSlot)
        {
            equipped.IsEquipped = false;
            equipped.UpdatedAt = DateTimeOffset.UtcNow;
        }

        entry.IsEquipped = true;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(context.RequestAborted);
        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var centralTime = CentralClock.Now;
        AuditLog.ItemEquipped(
            logger,
            character.Id,
            entry.ItemId,
            entry.Item.Name,
            accountId,
            centralTime);
        return Results.Ok(await BuildInventoryResponseAsync(character, database, context.RequestAborted));
    }

    private static async Task<IResult> UnequipItemAsync(
        Guid entryId,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before changing equipment."));
        }

        var entry = await database.CharacterInventory
            .SingleOrDefaultAsync(x => x.Id == entryId && x.CharacterId == character.Id, context.RequestAborted);
        if (entry is null)
        {
            return Results.NotFound(new ErrorResponse("Inventory item not found."));
        }

        entry.IsEquipped = false;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(context.RequestAborted);
        return Results.Ok(await BuildInventoryResponseAsync(character, database, context.RequestAborted));
    }

    private static async Task<IResult> UseItemAsync(
        Guid entryId,
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before using items."));
        }

        var entry = await database.CharacterInventory
            .Include(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == entryId && x.CharacterId == character.Id, context.RequestAborted);
        if (entry is null)
        {
            return Results.NotFound(new ErrorResponse("Inventory item not found."));
        }

        if (entry.Item.Kind != ItemKind.Consumable || entry.Item.HealingAmount <= 0)
        {
            return Results.Conflict(new ErrorResponse("That item cannot be used."));
        }

        if (character.Health >= character.MaximumHealth)
        {
            return Results.Conflict(new ErrorResponse("Health is already full."));
        }

        var before = character.Health;
        character.Health = Math.Min(character.MaximumHealth, character.Health + entry.Item.HealingAmount);
        character.UpdatedAt = DateTimeOffset.UtcNow;
        entry.Quantity -= 1;
        if (entry.Quantity <= 0)
        {
            database.CharacterInventory.Remove(entry);
        }
        else
        {
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await database.SaveChangesAsync(context.RequestAborted);
        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var centralTime = CentralClock.Now;
        AuditLog.ItemUsed(
            logger,
            character.Id,
            entry.ItemId,
            character.Health - before,
            accountId,
            centralTime);
        return Results.Ok(new ItemUseResponse(
            ToCharacterResponse(character),
            await BuildInventoryResponseAsync(character, database, context.RequestAborted),
            $"{character.Name} used {entry.Item.Name} and restored {character.Health - before} health."));
    }

    private static async Task<IResult> SellItemAsync(
        Guid entryId,
        SellItemRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before trading items."));
        }
        if (!IsValidWorldPosition(request.PlayerPosition.X, request.PlayerPosition.Y, request.PlayerPosition.Z))
        {
            return Results.BadRequest(new ErrorResponse("The trading position is invalid."));
        }

        var entry = await database.CharacterInventory
            .Include(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == entryId && x.CharacterId == character.Id, context.RequestAborted);
        if (entry is null)
        {
            return Results.NotFound(new ErrorResponse("Inventory item not found."));
        }
        if (entry.IsEquipped)
        {
            return Results.Conflict(new ErrorResponse("Unequip that item before selling it."));
        }

        var buyer = BuyerFor(entry.Item);
        if (!IsNearBuyer(request.PlayerPosition, buyer))
        {
            return Results.Conflict(new ErrorResponse(
                $"{buyer.Name} wants {BuyerInterest(entry.Item)}, but you must stand beside {buyer.Name} in Stonehaven to sell it."));
        }

        var goldReceived = Math.Max(1, entry.Item.BaseValue / 2);
        character.Gold += goldReceived;
        character.UpdatedAt = DateTimeOffset.UtcNow;
        entry.Quantity--;
        if (entry.Quantity <= 0)
        {
            database.CharacterInventory.Remove(entry);
        }
        else
        {
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await database.SaveChangesAsync(context.RequestAborted);

        return Results.Ok(new ItemSaleResponse(
            await BuildInventoryResponseAsync(character, database, context.RequestAborted),
            goldReceived,
            buyer.Name,
            $"{buyer.Name} bought one {entry.Item.Name} for {goldReceived} gold."));
    }

    private static async Task<IResult> GetSkillsAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before loading skills."));
        }

        await EnsurePhaseFiveLoadoutAsync(character, database, context.RequestAborted);
        var records = await database.CharacterSkills
            .Where(x => x.CharacterId == character.Id)
            .ToDictionaryAsync(x => x.SkillKey, context.RequestAborted);
        return Results.Ok(GetSkillDefinitions(character.Archetype)
            .Select(definition => ToSkillResponse(definition, records[definition.Key])));
    }

    private static async Task<IResult> UseSkillAsync(
        SkillUseRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        RaidSimulationService raidSimulation,
        FactionLeadershipService factionLeadership,
        ILoggerFactory loggerFactory)
    {
        var character = await GetSelectedCharacterAsync(context, database);
        if (character is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before using skills."));
        }

        await EnsurePhaseFiveLoadoutAsync(character, database, context.RequestAborted);
        var definition = GetSkillDefinitions(character.Archetype)
            .SingleOrDefault(x => x.Key.Equals(request.SkillKey, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return Results.NotFound(new ErrorResponse("Skill not found for this character."));
        }

        var skill = await database.CharacterSkills.SingleAsync(
            x => x.CharacterId == character.Id && x.SkillKey == definition.Key,
            context.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        if (skill.LastUsedAt is not null && (now - skill.LastUsedAt.Value).TotalSeconds < definition.CooldownSeconds)
        {
            var remaining = Math.Ceiling(definition.CooldownSeconds - (now - skill.LastUsedAt.Value).TotalSeconds);
            return Results.Json(new ErrorResponse($"{definition.Name} is ready in {remaining:0} seconds."), statusCode: 429);
        }

        if (!definition.IsOffensive)
        {
            if (character.Health >= character.MaximumHealth)
            {
                return Results.Conflict(new ErrorResponse("Health is already full."));
            }

            var before = character.Health;
            character.Health = Math.Min(character.MaximumHealth, character.Health + definition.Healing);
            character.UpdatedAt = now;
            skill.LastUsedAt = now;
            skill.Experience += 1;
            skill.UpdatedAt = now;
            await database.SaveChangesAsync(context.RequestAborted);
            var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
            var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
            var centralTime = CentralClock.Now;
            AuditLog.SkillUsed(logger, character.Id, definition.Key, null, 0, accountId, centralTime);
            return Results.Ok(new SkillUseResponse(
                ToCharacterResponse(character),
                null,
                definition.Key,
                0,
                character.Health - before,
                0,
                false,
                false,
                [],
                $"{character.Name} used {definition.Name} and restored {character.Health - before} health."));
        }

        if (request.CreatureId is null || request.CreaturePosition is null ||
            !IsValidWorldPosition(request.PlayerPosition.X, request.PlayerPosition.Y, request.PlayerPosition.Z) ||
            !IsValidWorldPosition(request.CreaturePosition.X, request.CreaturePosition.Y, request.CreaturePosition.Z))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["target"] = ["An offensive skill requires a valid creature target and playtest-world positions."]
            });
        }

        var creature = await database.Creatures
            .Include(x => x.Species)
            .SingleOrDefaultAsync(x => x.Id == request.CreatureId && x.RegionId == LivingRealmsDbContext.StonehavenValleyId,
                context.RequestAborted);
        if (creature is null)
        {
            return Results.NotFound(new ErrorResponse("Creature not found."));
        }

        RespawnCreatureIfReady(creature, now);
        if (creature.Status != CreatureStatus.Alive)
        {
            return Results.Conflict(new ErrorResponse(
                creature.FactionId is null
                    ? "That creature is defeated and has not respawned yet."
                    : "That named faction member is no longer active in the world."));
        }

        UpdateCharacterPosition(character, request.PlayerPosition, now);
        UpdateCreaturePosition(creature, request.CreaturePosition, now);
        if (Distance(request.PlayerPosition, request.CreaturePosition) > definition.Range)
        {
            return Results.Conflict(new ErrorResponse($"That target is outside {definition.Name}'s range."));
        }

        var bonuses = await GetEquipmentBonusesAsync(character.Id, database, context.RequestAborted);
        var baseAttack = character.Archetype == CharacterArchetype.Vanguard
            ? 26 + character.Level * 3
            : 20 + character.Level * 2;
        var damage = Math.Max(1, baseAttack + bonuses.AttackBonus + definition.Power - creature.Defense / 3);
        creature.Health = Math.Max(0, creature.Health - damage);
        creature.UpdatedAt = now;
        creature.LastProcessedAt = now;
        character.LastAttackAt = now;
        character.UpdatedAt = now;
        skill.LastUsedAt = now;
        skill.Experience += 1;
        skill.UpdatedAt = now;

        var defeated = creature.Health == 0;
        var experienceGained = 0;
        var leveledUp = false;
        RaidContributionResult? raidContribution = null;
        FactionDefeatResult? factionDefeat = null;
        IReadOnlyCollection<LootResponse> loot = [];
        if (defeated)
        {
            creature.PositionX = creature.SpawnX;
            creature.PositionY = creature.SpawnY;
            creature.PositionZ = creature.SpawnZ;
            experienceGained = Math.Max(1, creature.Species.ExperienceReward);
            character.Experience += experienceGained;
            leveledUp = ApplyLevelUps(character);
            loot = await AwardLootAsync(character, creature, database, context.RequestAborted);
            raidContribution = await raidSimulation.RegisterPlayerDefeatAsync(
                creature,
                character.Id,
                now,
                context.RequestAborted);
            if (raidContribution is null)
            {
                factionDefeat = await factionLeadership.ResolvePersistentDefeatAsync(
                    creature,
                    now,
                    character.Id,
                    cancellationToken: context.RequestAborted);
            }
        }

        await database.SaveChangesAsync(context.RequestAborted);
        var skillLogger = loggerFactory.CreateLogger("LivingRealms.Audit");
        var skillAccountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var skillCentralTime = CentralClock.Now;
        AuditLog.SkillUsed(skillLogger, character.Id, definition.Key, creature.Id, damage, skillAccountId, skillCentralTime);
        var message = defeated
            ? $"{character.Name} defeated {creature.Name} with {definition.Name}, gained {experienceGained} XP, and found {FormatLoot(loot)}."
            : $"{character.Name}'s {definition.Name} dealt {damage} damage to {creature.Name}.";
        if (raidContribution is not null)
        {
            message += $" Stonehaven gained {raidContribution.ContributionGained} raid strength from the victory.";
        }
        if (!string.IsNullOrWhiteSpace(factionDefeat?.ChronicleSummary))
        {
            message += $" {factionDefeat.ChronicleSummary}";
        }
        return Results.Ok(new SkillUseResponse(
            ToCharacterResponse(character),
            ToCreatureResponse(creature),
            definition.Key,
            damage,
            0,
            experienceGained,
            leveledUp,
            defeated,
            loot,
            message));
    }

    internal static async Task EnsurePhaseFiveLoadoutAsync(
        Character character,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var inventoryEntries = await database.CharacterInventory
            .Include(x => x.Item)
            .Where(x => x.CharacterId == character.Id)
            .ToListAsync(cancellationToken);
        var existingSkills = await database.CharacterSkills
            .Where(x => x.CharacterId == character.Id)
            .Select(x => x.SkillKey)
            .ToListAsync(cancellationToken);
        var initializingLoadout = existingSkills.Count == 0;
        var weaponId = character.Archetype == CharacterArchetype.Vanguard
            ? LivingRealmsDbContext.TrainingBladeItemId
            : LivingRealmsDbContext.HuntingBowItemId;
        if (!inventoryEntries.Any(x => x.ItemId == weaponId) &&
            (initializingLoadout || !inventoryEntries.Any(x => x.Item.EquipmentSlot == EquipmentSlot.Weapon)))
        {
            database.CharacterInventory.Add(CreateInventoryEntry(character.Id, weaponId, true));
            changed = true;
        }
        if (!inventoryEntries.Any(x => x.ItemId == LivingRealmsDbContext.LeatherGuardItemId) &&
            (initializingLoadout || !inventoryEntries.Any(x => x.Item.EquipmentSlot == EquipmentSlot.Armor)))
        {
            database.CharacterInventory.Add(CreateInventoryEntry(character.Id, LivingRealmsDbContext.LeatherGuardItemId, true));
            changed = true;
        }

        foreach (var definition in GetSkillDefinitions(character.Archetype).Where(x => !existingSkills.Contains(x.Key)))
        {
            database.CharacterSkills.Add(new CharacterSkill
            {
                CharacterId = character.Id,
                SkillKey = definition.Key,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            changed = true;
        }

        if (changed)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    internal static async Task<EquipmentBonuses> GetEquipmentBonusesAsync(
        Guid characterId,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var bonuses = await database.CharacterInventory
            .Where(x => x.CharacterId == characterId && x.IsEquipped && x.Item.EquipmentSlot != null)
            .Select(x => new
            {
                Slot = x.Item.EquipmentSlot!.Value,
                x.Item.AttackBonus,
                x.Item.DefenseBonus,
                x.Item.Rarity,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var activeSlots = bonuses
            .GroupBy(x => x.Slot)
            .Select(group => group
                .OrderByDescending(x => x.Slot == EquipmentSlot.Weapon ? x.AttackBonus : x.DefenseBonus)
                .ThenByDescending(x => x.Rarity)
                .ThenByDescending(x => x.UpdatedAt)
                .First())
            .ToArray();
        return new EquipmentBonuses(activeSlots.Sum(x => x.AttackBonus), activeSlots.Sum(x => x.DefenseBonus));
    }

    internal static async Task<IReadOnlyCollection<LootResponse>> AwardLootAsync(
        Character character,
        Creature creature,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var itemIds = (creature.SpeciesId switch
        {
            var id when id == LivingRealmsDbContext.ForestRatSpeciesId =>
                new[] { LivingRealmsDbContext.RatTailItemId, LivingRealmsDbContext.FieldTonicItemId },
            var id when id == LivingRealmsDbContext.PrairieWolfSpeciesId =>
                new[] { LivingRealmsDbContext.WolfPeltItemId },
            var id when id == LivingRealmsDbContext.GoblinRaiderSpeciesId =>
                new[] { character.Archetype == CharacterArchetype.Vanguard ? LivingRealmsDbContext.GoblinBladeItemId : LivingRealmsDbContext.RaiderBowItemId },
            var id when id == LivingRealmsDbContext.GoblinChiefSpeciesId =>
                new[]
                {
                    character.Archetype == CharacterArchetype.Vanguard ? LivingRealmsDbContext.ChiefWarbladeItemId : LivingRealmsDbContext.ChiefLongbowItemId,
                    LivingRealmsDbContext.FieldTonicItemId
                },
            _ => Array.Empty<Guid>()
        }).ToList();
        if (itemIds.Count == 0)
        {
            return [];
        }

        var items = await database.Items.Where(x => itemIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var existing = await database.CharacterInventory
            .Include(x => x.Item)
            .Where(x => x.CharacterId == character.Id && itemIds.Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, cancellationToken);
        var carriedWeight = await GetCarriedWeightAsync(character.Id, database, cancellationToken);
        var loot = new List<LootResponse>();
        foreach (var item in items)
        {
            if (carriedWeight + item.UnitWeight > character.CarryCapacity)
            {
                continue;
            }
            if (existing.TryGetValue(item.Id, out var entry))
            {
                entry.Quantity += 1;
                entry.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                database.CharacterInventory.Add(CreateInventoryEntry(character.Id, item.Id, false));
            }
            carriedWeight += item.UnitWeight;
            loot.Add(new LootResponse(item.Id, item.Key, item.Name, item.Rarity.ToString(), 1));
        }
        return loot;
    }

    internal static Task<int> GetCarriedWeightAsync(
        Guid characterId,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken) =>
        database.CharacterInventory
            .Where(x => x.CharacterId == characterId)
            .SumAsync(x => x.Quantity * x.Item.UnitWeight, cancellationToken);

    private static CharacterInventory CreateInventoryEntry(Guid characterId, Guid itemId, bool equipped) => new()
    {
        CharacterId = characterId,
        ItemId = itemId,
        Quantity = 1,
        IsEquipped = equipped,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task<InventoryResponse> BuildInventoryResponseAsync(
        Character character,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        if (await NormalizeEquippedSlotsAsync(character.Id, database, cancellationToken))
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        var entries = await database.CharacterInventory
            .AsNoTracking()
            .Include(x => x.Item)
            .Where(x => x.CharacterId == character.Id)
            .OrderByDescending(x => x.IsEquipped)
            .ThenBy(x => x.Item.Kind)
            .ThenBy(x => x.Item.Name)
            .ToListAsync(cancellationToken);
        var bonuses = new EquipmentBonuses(
            entries.Where(x => x.IsEquipped).Sum(x => x.Item.AttackBonus),
            entries.Where(x => x.IsEquipped).Sum(x => x.Item.DefenseBonus));
        var baseAttack = character.Archetype == CharacterArchetype.Vanguard
            ? 26 + character.Level * 3
            : 20 + character.Level * 2;
        var baseDefense = 6 + character.Level * 2;
        var usedCapacity = entries.Sum(x => x.Quantity * x.Item.UnitWeight);
        return new InventoryResponse(
            character.Id,
            baseAttack + bonuses.AttackBonus,
            baseDefense + bonuses.DefenseBonus,
            character.Gold,
            usedCapacity,
            character.CarryCapacity,
            entries.Select(ToInventoryItemResponse).ToArray());
    }

    private static async Task<bool> NormalizeEquippedSlotsAsync(
        Guid characterId,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var equipped = await database.CharacterInventory
            .Include(x => x.Item)
            .Where(x => x.CharacterId == characterId && x.IsEquipped && x.Item.EquipmentSlot != null)
            .ToListAsync(cancellationToken);
        var changed = false;
        foreach (var slotEntries in equipped.GroupBy(x => x.Item.EquipmentSlot!.Value))
        {
            var winner = slotEntries
                .OrderByDescending(x => slotEntries.Key == EquipmentSlot.Weapon
                    ? x.Item.AttackBonus
                    : x.Item.DefenseBonus)
                .ThenByDescending(x => x.Item.Rarity)
                .ThenByDescending(x => x.UpdatedAt)
                .First();
            foreach (var duplicate in slotEntries.Where(x => x.Id != winner.Id))
            {
                duplicate.IsEquipped = false;
                duplicate.UpdatedAt = DateTimeOffset.UtcNow;
                changed = true;
            }
        }
        return changed;
    }

    private static InventoryItemResponse ToInventoryItemResponse(CharacterInventory entry) => new(
        entry.Id,
        entry.ItemId,
        entry.Item.Key,
        entry.Item.Name,
        entry.Item.Description,
        entry.Item.Kind.ToString(),
        entry.Item.Rarity.ToString(),
        entry.Item.EquipmentSlot?.ToString(),
        entry.Item.RequiredArchetype?.ToString(),
        entry.Item.AttackBonus,
        entry.Item.DefenseBonus,
        entry.Item.HealingAmount,
        entry.Item.UnitWeight,
        entry.Item.UnitWeight * entry.Quantity,
        BuyerFor(entry.Item).Name,
        entry.Quantity,
        entry.IsEquipped);

    private static Buyer BuyerFor(Item item) => item.Kind switch
    {
        ItemKind.Weapon or ItemKind.Armor => new Buyer("Brann the Blacksmith", -11.0f, -9.2f, -15.0f, -17.0f),
        ItemKind.Consumable => new Buyer("Elowen the Healer", -12.0f, -22.6f, -16.0f, -29.0f),
        _ => new Buyer("Oren the Storekeeper", 12.0f, -23.6f, 16.0f, -30.0f)
    };

    private static string BuyerInterest(Item item) => item.Kind switch
    {
        ItemKind.Weapon or ItemKind.Armor => "weapons and armor",
        ItemKind.Consumable => "medicines and tonics",
        _ => "materials, trophies, and supplies"
    };

    private static bool IsNearBuyer(PositionRequest position, Buyer buyer) =>
        HorizontalDistance(position, buyer.WorkX, buyer.WorkZ) <= 6.0f ||
        HorizontalDistance(position, buyer.HomeX, buyer.HomeZ) <= 6.0f;

    private static float HorizontalDistance(PositionRequest position, float x, float z)
    {
        var dx = position.X - x;
        var dz = position.Z - z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static IReadOnlyCollection<SkillDefinition> GetSkillDefinitions(CharacterArchetype archetype) =>
        archetype == CharacterArchetype.Vanguard
            ?
            [
                new("shield-bash", "Shield Bash", "A crushing close-range strike with bonus damage.", "Q", 5, true, 3.2f, 13, 0),
                new("second-wind", "Second Wind", "Recover 32 health without using an item.", "E", 14, false, 0, 0, 32)
            ]
            :
            [
                new("piercing-shot", "Piercing Shot", "A long-range shot that cuts through armor.", "Q", 5, true, 22, 12, 0),
                new("field-dressing", "Field Dressing", "Recover 28 health while staying mobile.", "E", 14, false, 0, 0, 28)
            ];

    private static CharacterSkillResponse ToSkillResponse(SkillDefinition definition, CharacterSkill skill) => new(
        definition.Key,
        definition.Name,
        definition.Description,
        definition.Hotkey,
        definition.CooldownSeconds,
        definition.IsOffensive,
        definition.Range,
        skill.Level,
        skill.Experience,
        skill.LastUsedAt,
        skill.LastUsedAt?.AddSeconds(definition.CooldownSeconds));

    private static async Task<Character?> GetSelectedCharacterAsync(HttpContext context, LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var characterId = await database.PlayerSessions
            .Where(x => x.Id == sessionId && x.AccountId == accountId && x.CharacterId != null)
            .Select(x => x.CharacterId)
            .SingleOrDefaultAsync(context.RequestAborted);
        return characterId is null
            ? null
            : await database.Characters.Include(x => x.Region)
                .SingleOrDefaultAsync(x => x.Id == characterId.Value && x.AccountId == accountId, context.RequestAborted);
    }

    private static Guid GetRequiredId(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Authenticated principal is missing the {claimType} claim.");
    }

    private static bool IsValidWorldPosition(float x, float y, float z) =>
        float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) &&
        MathF.Abs(x) <= MaximumWorldCoordinate && y is >= -2.0f and <= MaximumWorldHeight &&
        MathF.Abs(z) <= MaximumWorldCoordinate;

    private static float Distance(PositionRequest a, PositionRequest b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return MathF.Sqrt(x * x + y * y + z * z);
    }

    private static void UpdateCharacterPosition(Character character, PositionRequest position, DateTimeOffset now)
    {
        character.PositionX = position.X;
        character.PositionY = position.Y;
        character.PositionZ = position.Z;
        character.UpdatedAt = now;
    }

    private static void UpdateCreaturePosition(Creature creature, PositionRequest position, DateTimeOffset now)
    {
        creature.PositionX = position.X;
        creature.PositionY = position.Y;
        creature.PositionZ = position.Z;
        creature.LastProcessedAt = now;
        creature.UpdatedAt = now;
    }

    private static void RespawnCreatureIfReady(Creature creature, DateTimeOffset now)
    {
        if (creature.FactionId is not null)
        {
            creature.RespawnAt = null;
            return;
        }
        if (creature.Status != CreatureStatus.Dead || creature.RespawnAt is null || creature.RespawnAt > now)
        {
            return;
        }
        creature.Status = CreatureStatus.Alive;
        creature.Health = creature.MaximumHealth;
        creature.PositionX = creature.SpawnX;
        creature.PositionY = creature.SpawnY;
        creature.PositionZ = creature.SpawnZ;
        creature.RespawnAt = null;
        creature.LastAttackAt = null;
        creature.LastProcessedAt = now;
        creature.UpdatedAt = now;
    }

    private static bool ApplyLevelUps(Character character)
    {
        var leveledUp = false;
        while (character.Experience >= character.Level * 100L)
        {
            character.Experience -= character.Level * 100L;
            character.Level += 1;
            character.MaximumHealth += 10;
            character.Health = character.MaximumHealth;
            leveledUp = true;
        }
        return leveledUp;
    }

    private static string FormatLoot(IReadOnlyCollection<LootResponse> loot) =>
        loot.Count == 0 ? "no loot" : string.Join(" and ", loot.Select(x => x.Name));

    private static CharacterResponse ToCharacterResponse(Character character) => new(
        character.Id, character.Name, character.Archetype.ToString(), character.Level, character.Experience,
        character.Health, character.MaximumHealth, character.Region?.Name ?? "Stonehaven Valley",
        new PositionResponse(character.PositionX, character.PositionY, character.PositionZ), character.UpdatedAt);

    private static CreatureResponse ToCreatureResponse(Creature creature) => new(
        creature.Id, creature.Species.Key, creature.Species.Name, creature.Name, creature.Title, creature.Role, creature.Level,
        creature.Health, creature.MaximumHealth, creature.Attack, creature.Defense, creature.MovementSpeed,
        creature.Species.DetectionRadius, creature.Species.AttackRange, creature.Aggression, creature.Status.ToString(),
        new PositionResponse(creature.PositionX, creature.PositionY, creature.PositionZ), creature.RespawnAt,
        creature.SpeciesId == LivingRealmsDbContext.GoblinChiefSpeciesId);

    private sealed record SkillDefinition(
        string Key, string Name, string Description, string Hotkey, double CooldownSeconds,
        bool IsOffensive, float Range, int Power, int Healing);
    private readonly record struct Buyer(string Name, float WorkX, float WorkZ, float HomeX, float HomeZ);

    public sealed record ErrorResponse(string Error);
    public sealed record PositionRequest(float X, float Y, float Z);
    public sealed record SkillUseRequest(string SkillKey, Guid? CreatureId, PositionRequest PlayerPosition, PositionRequest? CreaturePosition);
    public sealed record EquipmentBonuses(int AttackBonus, int DefenseBonus);
    public sealed record InventoryResponse(
        Guid CharacterId, int Attack, int Defense, int Gold, int UsedCapacity, int CarryCapacity,
        IReadOnlyCollection<InventoryItemResponse> Items);
    public sealed record InventoryItemResponse(
        Guid Id, Guid ItemId, string Key, string Name, string? Description, string Kind, string Rarity,
        string? EquipmentSlot, string? RequiredArchetype, int AttackBonus, int DefenseBonus, int HealingAmount,
        int UnitWeight, int TotalWeight, string BuyerName, int Quantity, bool IsEquipped);
    public sealed record LootResponse(Guid ItemId, string Key, string Name, string Rarity, int Quantity);
    public sealed record CharacterSkillResponse(
        string Key, string Name, string Description, string Hotkey, double CooldownSeconds, bool IsOffensive,
        float Range, int Level, long Experience, DateTimeOffset? LastUsedAt, DateTimeOffset? ReadyAt);
    public sealed record PositionResponse(float X, float Y, float Z);
    public sealed record CharacterResponse(
        Guid Id, string Name, string Archetype, int Level, long Experience, int Health, int MaximumHealth,
        string Region, PositionResponse Position, DateTimeOffset UpdatedAt);
    public sealed record CreatureResponse(
        Guid Id, string SpeciesKey, string SpeciesName, string Name, string? Title, string? Role, int Level, int Health,
        int MaximumHealth, int Attack, int Defense, float MovementSpeed, float DetectionRadius, float AttackRange,
        int Aggression, string Status, PositionResponse Position, DateTimeOffset? RespawnAt, bool IsBoss);
    public sealed record ItemUseResponse(CharacterResponse Character, InventoryResponse Inventory, string Message);
    public sealed record SellItemRequest(PositionRequest PlayerPosition);
    public sealed record ItemSaleResponse(
        InventoryResponse Inventory, int GoldReceived, string BuyerName, string Message);
    public sealed record SkillUseResponse(
        CharacterResponse Character, CreatureResponse? Creature, string SkillKey, int Damage, int Healed,
        int ExperienceGained, bool LeveledUp, bool CreatureDefeated, IReadOnlyCollection<LootResponse> Loot,
        string Message);
}
