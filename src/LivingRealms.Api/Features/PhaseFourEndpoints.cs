using System.Security.Claims;
using LivingRealms.Api.Logging;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class PhaseFourEndpoints
{
    private const float MaximumWorldCoordinate = 142.0f;
    private const float MaximumWorldHeight = 20.0f;

    public static IEndpointRouteBuilder MapPhaseFourEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var gameplay = endpoints.MapGroup("/api/v1").RequireAuthorization();
        gameplay.MapGet("/regions/stonehaven-valley/creatures", ListCreaturesAsync);
        gameplay.MapPut("/regions/stonehaven-valley/creatures/positions", SaveCreaturePositionsAsync)
            .RequireRateLimiting("gameplay");
        gameplay.MapPost("/combat/player-attack", PlayerAttackAsync)
            .RequireRateLimiting("gameplay");
        gameplay.MapPost("/combat/creature-attack", CreatureAttackAsync)
            .RequireRateLimiting("gameplay");
        gameplay.MapPost("/combat/settlement-defense-attack", SettlementDefenseAttackAsync)
            .RequireRateLimiting("gameplay");
        return endpoints;
    }

    private static async Task<IResult> ListCreaturesAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        WorldPopulationService population)
    {
        var selected = await GetSelectedCharacterAsync(context, database);
        if (selected is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before loading Stonehaven Valley creatures."));
        }

        await population.EnsureDarkwoodClanMembersAsync(cancellationToken: context.RequestAborted);
        await RespawnReadyCreaturesAsync(database, context.RequestAborted);
        var creatures = await database.Creatures
            .AsNoTracking()
            .Include(x => x.Species)
            .Where(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Name)
            .ToListAsync(context.RequestAborted);

        return Results.Ok(creatures.Select(ToCreatureResponse));
    }

    private static async Task<IResult> SaveCreaturePositionsAsync(
        CreaturePositionsRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var selected = await GetSelectedCharacterAsync(context, database);
        if (selected is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before saving creature positions."));
        }

        if (selected.LastAttackAt is not null && selected.LastAttackAt.Value > DateTimeOffset.UtcNow)
        {
            return Results.NoContent();
        }

        if (request.Creatures is null ||
            request.Creatures.Count is < 1 or > 32 ||
            request.Creatures.Any(position => !IsValidWorldPosition(position.X, position.Y, position.Z)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["creatures"] = ["Provide between 1 and 32 finite creature positions inside the Living Realms playtest world."]
            });
        }

        var updates = request.Creatures
            .GroupBy(x => x.Id)
            .ToDictionary(group => group.Key, group => group.Last());
        var creatures = await database.Creatures
            .Where(x => updates.Keys.Contains(x.Id) &&
                        x.RegionId == LivingRealmsDbContext.StonehavenValleyId &&
                        x.Status == CreatureStatus.Alive)
            .ToListAsync(context.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        foreach (var creature in creatures)
        {
            var position = updates[creature.Id];
            creature.PositionX = position.X;
            creature.PositionY = position.Y;
            creature.PositionZ = position.Z;
            creature.LastProcessedAt = now;
            creature.UpdatedAt = now;
        }

        await database.SaveChangesAsync(context.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> PlayerAttackAsync(
        CombatRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        RaidSimulationService raidSimulation,
        ILoggerFactory loggerFactory)
    {
        if (!IsValidWorldPosition(request.PlayerPosition.X, request.PlayerPosition.Y, request.PlayerPosition.Z) ||
            !IsValidWorldPosition(request.CreaturePosition.X, request.CreaturePosition.Y, request.CreaturePosition.Z))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["positions"] = ["Player and creature positions must be finite and inside the Living Realms playtest world."]
            });
        }

        var selected = await GetSelectedCharacterAsync(context, database);
        if (selected is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before attacking."));
        }

        var creature = await database.Creatures
            .Include(x => x.Species)
            .SingleOrDefaultAsync(
                x => x.Id == request.CreatureId &&
                     x.RegionId == LivingRealmsDbContext.StonehavenValleyId,
                context.RequestAborted);
        if (creature is null)
        {
            return Results.NotFound(new ErrorResponse("Creature not found."));
        }

        var now = DateTimeOffset.UtcNow;
        if (selected.LastAttackAt is not null && selected.LastAttackAt.Value > now)
        {
            return Results.Conflict(new ErrorResponse(
                "Recover beneath Stonehaven's sanctuary before attacking again."));
        }
        RespawnCreatureIfReady(creature, now);
        if (creature.Status != CreatureStatus.Alive)
        {
            return Results.Conflict(new ErrorResponse("That creature is defeated and has not respawned yet."));
        }

        var cooldown = selected.Archetype == CharacterArchetype.Vanguard ? 0.55 : 0.75;
        if (selected.LastAttackAt is not null &&
            (now - selected.LastAttackAt.Value).TotalSeconds < cooldown)
        {
            return Results.Json(
                new ErrorResponse("Your attack is still recovering."),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        UpdateCharacterPosition(selected, request.PlayerPosition, now);
        UpdateCreaturePosition(creature, request.CreaturePosition, now);
        var attackRange = selected.Archetype == CharacterArchetype.Vanguard ? 2.8f : 18.0f;
        var distance = Distance(
            request.PlayerPosition.X,
            request.PlayerPosition.Y,
            request.PlayerPosition.Z,
            creature.PositionX,
            creature.PositionY,
            creature.PositionZ);
        if (distance > attackRange)
        {
            return Results.Conflict(new ErrorResponse(
                selected.Archetype == CharacterArchetype.Vanguard
                    ? "Move closer before using Alden's melee attack."
                    : "That target is outside Elara's bow range."));
        }

        await PhaseFiveEndpoints.EnsurePhaseFiveLoadoutAsync(selected, database, context.RequestAborted);
        var equipment = await PhaseFiveEndpoints.GetEquipmentBonusesAsync(selected.Id, database, context.RequestAborted);
        var attackPower = (selected.Archetype == CharacterArchetype.Vanguard
            ? 26 + selected.Level * 3
            : 20 + selected.Level * 2) + equipment.AttackBonus;
        var damage = Math.Max(1, attackPower - creature.Defense / 2);
        creature.Health = Math.Max(0, creature.Health - damage);
        creature.UpdatedAt = now;
        creature.LastProcessedAt = now;
        selected.LastAttackAt = now;
        selected.UpdatedAt = now;

        var defeated = creature.Health == 0;
        var experienceGained = 0;
        var leveledUp = false;
        RaidContributionResult? raidContribution = null;
        IReadOnlyCollection<PhaseFiveEndpoints.LootResponse> loot = [];
        if (defeated)
        {
            creature.Status = CreatureStatus.Dead;
            creature.RespawnAt = now.AddSeconds(Math.Max(15, creature.Species.RespawnSeconds));
            creature.PositionX = creature.SpawnX;
            creature.PositionY = creature.SpawnY;
            creature.PositionZ = creature.SpawnZ;
            experienceGained = Math.Max(1, creature.Species.ExperienceReward);
            selected.Experience += experienceGained;
            leveledUp = ApplyLevelUps(selected);
            loot = await PhaseFiveEndpoints.AwardLootAsync(selected, creature, database, context.RequestAborted);
            raidContribution = await raidSimulation.RegisterPlayerDefeatAsync(
                creature,
                selected.Id,
                now,
                context.RequestAborted);
        }

        await database.SaveChangesAsync(context.RequestAborted);

        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.CreatureDamaged(
            logger,
            selected.Id,
            creature.Id,
            damage,
            accountId,
            CentralClock.Now);
        if (defeated)
        {
            AuditLog.CreatureDefeated(
                logger,
                selected.Id,
                creature.Id,
                experienceGained,
                selected.Level,
                accountId,
                CentralClock.Now);
        }

        var message = defeated
            ? leveledUp
                ? $"{selected.Name} defeated {creature.Name}, gained {experienceGained} XP, reached level {selected.Level}, and found {FormatLoot(loot)}!"
                : $"{selected.Name} defeated {creature.Name}, gained {experienceGained} XP, and found {FormatLoot(loot)}."
            : $"{selected.Name} dealt {damage} damage to {creature.Name}.";
        if (raidContribution is not null)
        {
            message += raidContribution.Status == SettlementRaidStatus.AttackersWon
                ? " Another surviving raider was cleared from Stonehaven."
                : $" Stonehaven gained {raidContribution.ContributionGained} raid strength from the victory.";
        }
        return Results.Ok(new CombatResponse(
            ToCharacterResponse(selected),
            ToCreatureResponse(creature),
            damage,
            experienceGained,
            leveledUp,
            defeated,
            false,
            loot,
            message));
    }

    private static async Task<IResult> CreatureAttackAsync(
        CombatRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        ILoggerFactory loggerFactory)
    {
        if (!IsValidWorldPosition(request.PlayerPosition.X, request.PlayerPosition.Y, request.PlayerPosition.Z) ||
            !IsValidWorldPosition(request.CreaturePosition.X, request.CreaturePosition.Y, request.CreaturePosition.Z))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["positions"] = ["Player and creature positions must be finite and inside the Living Realms playtest world."]
            });
        }

        var selected = await GetSelectedCharacterAsync(context, database);
        if (selected is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before resolving creature combat."));
        }

        var creature = await database.Creatures
            .Include(x => x.Species)
            .SingleOrDefaultAsync(
                x => x.Id == request.CreatureId &&
                     x.RegionId == LivingRealmsDbContext.StonehavenValleyId,
                context.RequestAborted);
        if (creature is null)
        {
            return Results.NotFound(new ErrorResponse("Creature not found."));
        }

        var now = DateTimeOffset.UtcNow;
        if (selected.LastAttackAt is not null && selected.LastAttackAt.Value > now)
        {
            return Results.Conflict(new ErrorResponse(
                "Stonehaven's sanctuary still protects this character from creature attacks."));
        }
        RespawnCreatureIfReady(creature, now);
        if (creature.Status != CreatureStatus.Alive)
        {
            return Results.Conflict(new ErrorResponse("A defeated creature cannot attack."));
        }

        if (creature.LastAttackAt is not null &&
            (now - creature.LastAttackAt.Value).TotalSeconds < 1.35)
        {
            return Results.Json(
                new ErrorResponse("The creature is still recovering from its previous attack."),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        UpdateCharacterPosition(selected, request.PlayerPosition, now);
        UpdateCreaturePosition(creature, request.CreaturePosition, now);
        var distance = Distance(
            request.PlayerPosition.X,
            request.PlayerPosition.Y,
            request.PlayerPosition.Z,
            creature.PositionX,
            creature.PositionY,
            creature.PositionZ);
        if (distance > creature.Species.AttackRange + 1.0f)
        {
            return Results.Conflict(new ErrorResponse("The creature is not close enough to attack."));
        }

        await PhaseFiveEndpoints.EnsurePhaseFiveLoadoutAsync(selected, database, context.RequestAborted);
        var equipment = await PhaseFiveEndpoints.GetEquipmentBonusesAsync(selected.Id, database, context.RequestAborted);
        var characterDefense = 6 + selected.Level * 2 + equipment.DefenseBonus;
        var damage = Math.Max(1, creature.Attack - characterDefense);
        selected.Health = Math.Max(0, selected.Health - damage);
        creature.LastAttackAt = now;
        creature.LastProcessedAt = now;
        creature.UpdatedAt = now;
        var knockedOut = selected.Health == 0;
        if (knockedOut)
        {
            selected.Health = selected.MaximumHealth;
            selected.PositionX = selected.Archetype == CharacterArchetype.Ranger ? 2.0f : -2.0f;
            selected.PositionY = 0.08f;
            selected.PositionZ = 8.0f;
            selected.LastAttackAt = now.AddSeconds(8);
            creature.PositionX = creature.SpawnX;
            creature.PositionY = creature.SpawnY;
            creature.PositionZ = creature.SpawnZ;
        }
        selected.UpdatedAt = now;

        await database.SaveChangesAsync(context.RequestAborted);

        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var logger = loggerFactory.CreateLogger("LivingRealms.Audit");
        AuditLog.CharacterDamaged(
            logger,
            creature.Id,
            selected.Id,
            damage,
            accountId,
            CentralClock.Now);
        if (knockedOut)
        {
            AuditLog.CharacterKnockedOut(
                logger,
                selected.Id,
                creature.Id,
                accountId,
                CentralClock.Now);
        }

        var message = knockedOut
            ? $"{selected.Name} was knocked out by {creature.Name} and returned to Stonehaven's gate."
            : $"{creature.Name} dealt {damage} damage to {selected.Name}.";
        return Results.Ok(new CombatResponse(
            ToCharacterResponse(selected),
            ToCreatureResponse(creature),
            damage,
            0,
            false,
            false,
            knockedOut,
            [],
            message));
    }

    private static async Task<IResult> SettlementDefenseAttackAsync(
        SettlementDefenseAttackRequest request,
        HttpContext context,
        LivingRealmsDbContext database)
    {
        if (!IsValidWorldPosition(
                request.ResidentPosition.X,
                request.ResidentPosition.Y,
                request.ResidentPosition.Z) ||
            !IsValidWorldPosition(
                request.CreaturePosition.X,
                request.CreaturePosition.Y,
                request.CreaturePosition.Z))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["positions"] = ["Resident and creature positions must be finite and inside the Living Realms playtest world."]
            });
        }

        if (await GetSelectedCharacterAsync(context, database) is null)
        {
            return Results.Conflict(new ErrorResponse("Select a character before resolving Stonehaven defense combat."));
        }

        var resident = await database.SettlementResidents.SingleOrDefaultAsync(
            x => x.Id == request.ResidentId &&
                 x.SettlementId == LivingRealmsDbContext.StonehavenVillageId,
            context.RequestAborted);
        if (resident is null || !resident.CanFight || resident.Status == ResidentStatus.Dead)
        {
            return Results.Conflict(new ErrorResponse("That resident cannot defend Stonehaven."));
        }

        var creature = await database.Creatures
            .Include(x => x.Species)
            .SingleOrDefaultAsync(
                x => x.Id == request.CreatureId &&
                     x.RegionId == LivingRealmsDbContext.StonehavenValleyId,
                context.RequestAborted);
        if (creature is null)
        {
            return Results.NotFound(new ErrorResponse("Creature not found."));
        }

        var now = DateTimeOffset.UtcNow;
        RespawnCreatureIfReady(creature, now);
        if (creature.Status != CreatureStatus.Alive)
        {
            return Results.Conflict(new ErrorResponse("That threat has already been defeated."));
        }

        UpdateCreaturePosition(creature, request.CreaturePosition, now);
        var distance = Distance(
            request.ResidentPosition.X,
            request.ResidentPosition.Y,
            request.ResidentPosition.Z,
            creature.PositionX,
            creature.PositionY,
            creature.PositionZ);
        if (distance > 3.25f ||
            MathF.Abs(creature.PositionX) > 31.0f ||
            creature.PositionZ is < -38.0f or > 9.0f)
        {
            return Results.Conflict(new ErrorResponse("The threat is outside Stonehaven's defensive reach."));
        }

        var guardPower = 22 + resident.MaximumHealth / 9;
        var damage = Math.Max(2, guardPower - creature.Defense / 3);
        creature.Health = Math.Max(0, creature.Health - damage);
        creature.UpdatedAt = now;
        creature.LastProcessedAt = now;
        var defeated = creature.Health == 0;
        if (defeated)
        {
            creature.Status = CreatureStatus.Dead;
            creature.RespawnAt = now.AddSeconds(Math.Max(15, creature.Species.RespawnSeconds));
            creature.PositionX = creature.SpawnX;
            creature.PositionY = creature.SpawnY;
            creature.PositionZ = creature.SpawnZ;
        }

        await database.SaveChangesAsync(context.RequestAborted);
        return Results.Ok(new SettlementDefenseResponse(
            ToCreatureResponse(creature),
            damage,
            defeated,
            defeated
                ? $"{resident.Name} defeated {creature.Name} in defense of Stonehaven."
                : $"{resident.Name} struck {creature.Name} for {damage} damage."));
    }

    private static async Task<Character?> GetSelectedCharacterAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        var characterId = await database.PlayerSessions
            .Where(x => x.Id == sessionId && x.AccountId == accountId && x.CharacterId != null)
            .Select(x => x.CharacterId)
            .SingleOrDefaultAsync(context.RequestAborted);
        if (characterId is null)
        {
            return null;
        }

        return await database.Characters
            .Include(x => x.Region)
            .SingleOrDefaultAsync(
                x => x.Id == characterId.Value && x.AccountId == accountId,
                context.RequestAborted);
    }

    private static async Task RespawnReadyCreaturesAsync(
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var defeated = await database.Creatures
            .Include(x => x.Species)
            .Where(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId &&
                        x.Status == CreatureStatus.Dead &&
                        x.RespawnAt != null &&
                        x.RespawnAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var creature in defeated)
        {
            RespawnCreatureIfReady(creature, now);
        }

        if (defeated.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static void RespawnCreatureIfReady(Creature creature, DateTimeOffset now)
    {
        if (creature.Status != CreatureStatus.Dead ||
            creature.RespawnAt is null ||
            creature.RespawnAt > now)
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

    private static void UpdateCharacterPosition(
        Character character,
        PositionRequest position,
        DateTimeOffset now)
    {
        character.PositionX = position.X;
        character.PositionY = position.Y;
        character.PositionZ = position.Z;
        character.UpdatedAt = now;
    }

    private static void UpdateCreaturePosition(
        Creature creature,
        PositionRequest position,
        DateTimeOffset now)
    {
        creature.PositionX = position.X;
        creature.PositionY = position.Y;
        creature.PositionZ = position.Z;
        creature.LastProcessedAt = now;
        creature.UpdatedAt = now;
    }

    private static bool IsValidWorldPosition(float x, float y, float z) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        float.IsFinite(z) &&
        MathF.Abs(x) <= MaximumWorldCoordinate &&
        y is >= -2.0f and <= MaximumWorldHeight &&
        MathF.Abs(z) <= MaximumWorldCoordinate;

    private static float Distance(float ax, float ay, float az, float bx, float by, float bz)
    {
        var x = ax - bx;
        var y = ay - by;
        var z = az - bz;
        return MathF.Sqrt(x * x + y * y + z * z);
    }

    private static string FormatLoot(IReadOnlyCollection<PhaseFiveEndpoints.LootResponse> loot) =>
        loot.Count == 0 ? "no loot" : string.Join(" and ", loot.Select(x => x.Name));

    private static Guid GetRequiredId(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Authenticated principal is missing the {claimType} claim.");
    }

    private static CharacterResponse ToCharacterResponse(Character character)
    {
        return new CharacterResponse(
            character.Id,
            character.Name,
            character.Archetype.ToString(),
            character.Level,
            character.Experience,
            character.Health,
            character.MaximumHealth,
            character.Region?.Name ?? "Stonehaven Valley",
            new PositionResponse(character.PositionX, character.PositionY, character.PositionZ),
            character.UpdatedAt);
    }

    private static CreatureResponse ToCreatureResponse(Creature creature)
    {
        return new CreatureResponse(
            creature.Id,
            creature.Species.Key,
            creature.Species.Name,
            creature.Name,
            creature.Title,
            creature.Role,
            creature.Level,
            creature.Health,
            creature.MaximumHealth,
            creature.Attack,
            creature.Defense,
            creature.MovementSpeed,
            creature.Species.DetectionRadius,
            creature.Species.AttackRange,
            creature.Aggression,
            creature.Status.ToString(),
            new PositionResponse(creature.PositionX, creature.PositionY, creature.PositionZ),
            new PositionResponse(creature.SpawnX, creature.SpawnY, creature.SpawnZ),
            creature.RespawnAt,
            creature.SpeciesId == LivingRealmsDbContext.GoblinChiefSpeciesId);
    }

    public sealed record ErrorResponse(string Error);
    public sealed record PositionRequest(float X, float Y, float Z);
    public sealed record CreaturePositionUpdate(Guid Id, float X, float Y, float Z);
    public sealed record CreaturePositionsRequest(IReadOnlyCollection<CreaturePositionUpdate>? Creatures);
    public sealed record CombatRequest(
        Guid CreatureId,
        PositionRequest PlayerPosition,
        PositionRequest CreaturePosition);
    public sealed record SettlementDefenseAttackRequest(
        Guid ResidentId,
        Guid CreatureId,
        PositionRequest ResidentPosition,
        PositionRequest CreaturePosition);
    public sealed record PositionResponse(float X, float Y, float Z);
    public sealed record CharacterResponse(
        Guid Id,
        string Name,
        string Archetype,
        int Level,
        long Experience,
        int Health,
        int MaximumHealth,
        string Region,
        PositionResponse Position,
        DateTimeOffset UpdatedAt);
    public sealed record CreatureResponse(
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
        PositionResponse Position,
        PositionResponse SpawnPosition,
        DateTimeOffset? RespawnAt,
        bool IsBoss);
    public sealed record CombatResponse(
        CharacterResponse Character,
        CreatureResponse Creature,
        int Damage,
        int ExperienceGained,
        bool LeveledUp,
        bool CreatureDefeated,
        bool CharacterKnockedOut,
        IReadOnlyCollection<PhaseFiveEndpoints.LootResponse> Loot,
        string Message);
    public sealed record SettlementDefenseResponse(
        CreatureResponse Creature,
        int Damage,
        bool CreatureDefeated,
        string Message);
}
