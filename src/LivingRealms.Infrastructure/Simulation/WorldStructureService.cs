using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed class WorldStructureService(LivingRealmsDbContext database)
{
    public async Task<IReadOnlyList<WorldStructureState>> GetStatesAsync(
        ResourceOwner? owner = null,
        CancellationToken cancellationToken = default)
    {
        var developmentStage = await database.Factions.AsNoTracking()
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.DevelopmentStage)
            .SingleAsync(cancellationToken);
        var query = database.WorldStructures.AsNoTracking()
            .Include(x => x.ConstructionProject)
            .AsQueryable();
        if (owner is not null)
        {
            query = query.Where(x => x.Owner == owner.Value);
        }

        var structures = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        return structures
            .Select(x => ToState(x, developmentStage))
            .ToArray();
    }

    public async Task<int> GetRemainingHealthAsync(
        ResourceOwner owner,
        CancellationToken cancellationToken = default)
    {
        var states = await GetStatesAsync(owner, cancellationToken);
        return states.Where(x => x.IsBuilt).Sum(x => x.Health);
    }

    public async Task<WorldStructureDamageResult> DamageOwnerAsync(
        ResourceOwner owner,
        int rawDamage,
        DateTimeOffset damagedAt,
        CancellationToken cancellationToken = default)
    {
        if (rawDamage <= 0)
        {
            return new WorldStructureDamageResult(null, 0, false, await GetRemainingHealthAsync(owner, cancellationToken));
        }

        var developmentStage = await database.Factions
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.DevelopmentStage)
            .SingleAsync(cancellationToken);
        var structures = await database.WorldStructures
            .Include(x => x.ConstructionProject)
            .Where(x => x.Owner == owner && x.Health > 0)
            .ToArrayAsync(cancellationToken);
        var target = structures
            .Where(x => IsBuilt(x, developmentStage))
            .OrderBy(AttackPriority)
            .ThenBy(x => x.Health)
            .ThenBy(x => x.DisplayOrder)
            .FirstOrDefault();
        if (target is null)
        {
            return new WorldStructureDamageResult(null, 0, false, 0);
        }

        var effectiveDamage = Math.Max(1, rawDamage - target.Armor);
        var appliedDamage = Math.Min(target.Health, effectiveDamage);
        target.Health -= appliedDamage;
        target.LastDamagedAt = damagedAt;
        target.DestroyedAt = target.Health == 0 ? damagedAt : null;
        target.UpdatedAt = damagedAt;
        await database.SaveChangesAsync(cancellationToken);

        var remainingHealth = structures
            .Where(x => IsBuilt(x, developmentStage))
            .Sum(x => x.Health);
        return new WorldStructureDamageResult(
            target.Key,
            appliedDamage,
            target.Health == 0,
            remainingHealth);
    }

    public async Task<WorldStructureDamageResult?> DamageStructureAsync(
        string key,
        int rawDamage,
        DateTimeOffset damagedAt,
        CancellationToken cancellationToken = default)
    {
        var developmentStage = await database.Factions
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.DevelopmentStage)
            .SingleAsync(cancellationToken);
        var target = await database.WorldStructures
            .Include(x => x.ConstructionProject)
            .SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (target is null || !IsBuilt(target, developmentStage))
        {
            return null;
        }

        var effectiveDamage = Math.Max(1, rawDamage - target.Armor);
        var appliedDamage = Math.Min(target.Health, effectiveDamage);
        target.Health -= appliedDamage;
        target.LastDamagedAt = damagedAt;
        target.DestroyedAt = target.Health == 0 ? damagedAt : null;
        target.UpdatedAt = damagedAt;
        await database.SaveChangesAsync(cancellationToken);
        return new WorldStructureDamageResult(
            target.Key,
            appliedDamage,
            target.Health == 0,
            await GetRemainingHealthAsync(target.Owner, cancellationToken));
    }

    public async Task ResetAsync(
        DateTimeOffset resetAt,
        CancellationToken cancellationToken = default)
    {
        var structures = await database.WorldStructures.ToArrayAsync(cancellationToken);
        foreach (var structure in structures)
        {
            structure.Health = structure.MaximumHealth;
            structure.LastDamagedAt = null;
            structure.DestroyedAt = null;
            structure.UpdatedAt = resetAt;
        }
    }

    public static WorldStructureState ToState(WorldStructure structure, int developmentStage)
    {
        var built = IsBuilt(structure, developmentStage);
        var health = built ? Math.Clamp(structure.Health, 0, structure.MaximumHealth) : 0;
        var maximumHealth = Math.Max(1, structure.MaximumHealth);
        var status = !built
            ? "Unbuilt"
            : health == 0
                ? "Destroyed"
                : structure.Kind is WorldStructureKind.Wall or WorldStructureKind.Gate &&
                  health <= maximumHealth / 4
                    ? "Breached"
                    : health <= maximumHealth / 4
                        ? "Critical"
                        : health < maximumHealth
                            ? "Damaged"
                            : "Healthy";
        return new WorldStructureState(
            structure.Id,
            structure.Key,
            structure.Name,
            structure.Owner.ToString(),
            structure.Kind.ToString(),
            health,
            maximumHealth,
            structure.Armor,
            built,
            built && health > 0,
            status,
            structure.ConstructionProject?.CurrentLevel ?? 0,
            new WorldPosition(structure.PositionX, structure.PositionY, structure.PositionZ),
            structure.LastDamagedAt,
            structure.DestroyedAt);
    }

    public static bool IsBuilt(WorldStructure structure, int developmentStage) =>
        developmentStage >= structure.RequiredDevelopmentStage &&
        (structure.ConstructionProjectId is null ||
         structure.ConstructionProject is not null &&
         structure.ConstructionProject.CurrentLevel >= structure.RequiredProjectLevel);

    private static int AttackPriority(WorldStructure structure) => structure.Kind switch
    {
        WorldStructureKind.Wall => 0,
        WorldStructureKind.Gate => 1,
        WorldStructureKind.Mine => 2,
        WorldStructureKind.Farm => 3,
        WorldStructureKind.Stockpile => 4,
        WorldStructureKind.Building => 5,
        WorldStructureKind.Dock => 6,
        _ => 7
    };
}

public sealed record WorldStructureState(
    Guid Id,
    string Key,
    string Name,
    string Owner,
    string Kind,
    int Health,
    int MaximumHealth,
    int Armor,
    bool IsBuilt,
    bool BlocksMovement,
    string Status,
    int ProjectLevel,
    WorldPosition Position,
    DateTimeOffset? LastDamagedAt,
    DateTimeOffset? DestroyedAt);

public sealed record WorldPosition(float X, float Y, float Z);

public sealed record WorldStructureDamageResult(
    string? StructureKey,
    int DamageApplied,
    bool Destroyed,
    int OwnerHealthRemaining);
