using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LivingRealms.Infrastructure.Simulation;

public sealed partial class SettlementRecoveryService(
    LivingRealmsDbContext database,
    WorldPopulationService population,
    ILogger<SettlementRecoveryService> logger)
{
    public static readonly TimeSpan DefeatDelay = TimeSpan.FromMinutes(15);
    private const int StonehavenRepairPointsPerWorldHour = 500;
    private const int DarkwoodRepairPointsPerWorldHour = 400;

    private static readonly Guid[] StonehavenFounderIds =
    [
        LivingRealmsDbContext.StonehavenLeaderResidentId,
        LivingRealmsDbContext.MiraResidentId,
        LivingRealmsDbContext.TomasResidentId,
        LivingRealmsDbContext.BrannResidentId,
        LivingRealmsDbContext.ElowenResidentId,
        LivingRealmsDbContext.OrenResidentId,
        LivingRealmsDbContext.NessaResidentId,
        LivingRealmsDbContext.DainResidentId,
        LivingRealmsDbContext.AvelineResidentId,
        LivingRealmsDbContext.CedricResidentId,
        LivingRealmsDbContext.YsabelResidentId
    ];

    private static readonly Guid[] DarkwoodFounderIds =
    [
        LivingRealmsDbContext.GoblinChiefCreatureId,
        Guid.Parse("9230414d-a60d-46ca-9c59-36cc3b867201"),
        Guid.Parse("9230414d-a60d-46ca-9c59-36cc3b867202"),
        Guid.Parse("74000000-0000-4000-8000-000000000001"),
        Guid.Parse("74000000-0000-4000-8000-000000000002"),
        Guid.Parse("74000000-0000-4000-8000-000000000003"),
        Guid.Parse("74000000-0000-4000-8000-000000000004")
    ];

    public async Task MarkDefeatedAsync(
        ResourceOwner owner,
        DateTimeOffset defeatedAt,
        CancellationToken cancellationToken = default)
    {
        var recovery = await database.SettlementRecoveries
            .SingleAsync(x => x.Owner == owner, cancellationToken);
        if (recovery.Status is SettlementRecoveryStatus.Defeated or SettlementRecoveryStatus.Rebuilding)
        {
            return;
        }

        var structures = await database.WorldStructures
            .Where(x => x.Owner == owner)
            .ToArrayAsync(cancellationToken);
        foreach (var structure in structures.Where(x => x.Health > 0))
        {
            structure.Health = 0;
            structure.LastDamagedAt = defeatedAt;
            structure.DestroyedAt = defeatedAt;
            structure.UpdatedAt = defeatedAt;
        }

        recovery.Status = SettlementRecoveryStatus.Defeated;
        recovery.FoundingPopulation = owner == ResourceOwner.Stonehaven
            ? WorldPopulationService.StartingStonehavenPopulation
            : WorldPopulationService.StartingDarkwoodPopulation;
        recovery.DefeatedAt = defeatedAt;
        recovery.RecoveryEligibleAt = defeatedAt.Add(DefeatDelay);
        recovery.RebuildingStartedAt = null;
        recovery.LastProgressedAt = defeatedAt;
        recovery.RecoveredAt = null;
        recovery.CurrentStructureKey = null;
        recovery.RebuildCycles = 0;
        recovery.UpdatedAt = defeatedAt;

        if (owner == ResourceOwner.Stonehaven)
        {
            await MarkStonehavenDefeatedAsync(defeatedAt, cancellationToken);
        }
        else
        {
            await MarkDarkwoodDefeatedAsync(defeatedAt, cancellationToken);
        }

        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "settlement_defeated",
            Title = $"{OwnerName(owner)} was completely destroyed",
            Description =
                $"{OwnerName(owner)} has no standing structures and no active population. " +
                $"It will remain defeated for fifteen real minutes before its founding population returns to rebuild functional structures first and walls last.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = owner == ResourceOwner.Darkwood
                ? LivingRealmsDbContext.DarkwoodClanId
                : null,
            OccurredAt = defeatedAt,
            ImportanceLevel = 5,
            CreatedAt = defeatedAt,
            UpdatedAt = defeatedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        LogSettlementDefeated(logger, owner, defeatedAt);
    }

    public async Task<IReadOnlyList<SettlementRecoveryState>> AdvanceAsync(
        DateTimeOffset advancedAt,
        int worldHours = 0,
        CancellationToken cancellationToken = default)
    {
        var recoveries = await database.SettlementRecoveries
            .OrderBy(x => x.Owner)
            .ToArrayAsync(cancellationToken);

        foreach (var recovery in recoveries)
        {
            if (recovery.Status == SettlementRecoveryStatus.Defeated &&
                recovery.RecoveryEligibleAt is not null &&
                advancedAt >= recovery.RecoveryEligibleAt.Value)
            {
                await BeginRebuildingAsync(recovery, advancedAt, cancellationToken);
            }

            if (recovery.Status == SettlementRecoveryStatus.Rebuilding && worldHours > 0)
            {
                await AdvanceRebuildingAsync(recovery, advancedAt, worldHours, cancellationToken);
            }
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        return await BuildStatesAsync(advancedAt, cancellationToken);
    }

    public async Task<IReadOnlyList<SettlementRecoveryState>> GetStatesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await AdvanceAsync(now, cancellationToken: cancellationToken);

    public async Task ResetAsync(
        DateTimeOffset resetAt,
        CancellationToken cancellationToken = default)
    {
        var recoveries = await database.SettlementRecoveries.ToArrayAsync(cancellationToken);
        foreach (var recovery in recoveries)
        {
            recovery.Status = SettlementRecoveryStatus.Healthy;
            recovery.FoundingPopulation = recovery.Owner == ResourceOwner.Stonehaven
                ? WorldPopulationService.StartingStonehavenPopulation
                : WorldPopulationService.StartingDarkwoodPopulation;
            recovery.DefeatedAt = null;
            recovery.RecoveryEligibleAt = null;
            recovery.RebuildingStartedAt = null;
            recovery.LastProgressedAt = null;
            recovery.RecoveredAt = null;
            recovery.CurrentStructureKey = null;
            recovery.RebuildCycles = 0;
            recovery.UpdatedAt = resetAt;
        }
    }

    private async Task MarkStonehavenDefeatedAsync(
        DateTimeOffset defeatedAt,
        CancellationToken cancellationToken)
    {
        var settlement = await database.Settlements
            .Include(x => x.Residents)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        settlement.Population = 0;
        settlement.StructuralIntegrity = 0;
        settlement.GuardStrength = 0;
        settlement.DefenseRating = 0;
        settlement.IsDestroyed = true;
        settlement.UpdatedAt = defeatedAt;
        foreach (var resident in settlement.Residents.Where(x =>
                     x.Health > 0 &&
                     x.Status is ResidentStatus.Active or ResidentStatus.Injured))
        {
            resident.Status = ResidentStatus.Missing;
            resident.UpdatedAt = defeatedAt;
        }
    }

    private async Task MarkDarkwoodDefeatedAsync(
        DateTimeOffset defeatedAt,
        CancellationToken cancellationToken)
    {
        var faction = await database.Factions
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        faction.Population = 0;
        faction.MilitaryStrength = 0;
        faction.Morale = 0;
        faction.UpdatedAt = defeatedAt;
        var creatures = await database.Creatures
            .Where(x => x.FactionId == faction.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var creature in creatures.Where(x =>
                     x.Status == CreatureStatus.Alive && x.Health > 0))
        {
            creature.Health = 0;
            creature.Status = CreatureStatus.Retired;
            creature.RespawnAt = null;
            creature.UpdatedAt = defeatedAt;
            creature.LastProcessedAt = defeatedAt;
        }
    }

    private async Task BeginRebuildingAsync(
        SettlementRecovery recovery,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        if (recovery.Owner == ResourceOwner.Stonehaven)
        {
            await RestoreStonehavenFoundersAsync(startedAt, cancellationToken);
        }
        else
        {
            await RestoreDarkwoodFoundersAsync(startedAt, cancellationToken);
        }

        recovery.Status = SettlementRecoveryStatus.Rebuilding;
        recovery.RebuildingStartedAt = startedAt;
        recovery.LastProgressedAt = startedAt;
        recovery.CurrentStructureKey = null;
        recovery.UpdatedAt = startedAt;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "settlement_rebuilding_started",
            Title = $"{OwnerName(recovery.Owner)}'s founders returned",
            Description =
                $"{recovery.FoundingPopulation} founding members returned with basic food and building supplies. " +
                "They will restore stockpiles, farms, workshops, the mine, and other functional structures before rebuilding gates and walls.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = recovery.Owner == ResourceOwner.Darkwood
                ? LivingRealmsDbContext.DarkwoodClanId
                : null,
            OccurredAt = startedAt,
            ImportanceLevel = 5,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        });
        await database.SaveChangesAsync(cancellationToken);

        if (recovery.Owner == ResourceOwner.Darkwood)
        {
            await population.EnsureDarkwoodClanMembersAsync(cancellationToken: cancellationToken);
        }
        else
        {
            await population.EnsureStonehavenResidentsAsync(cancellationToken: cancellationToken);
        }
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogRebuildingStarted(logger, recovery.Owner, startedAt);
        }
    }

    private async Task RestoreStonehavenFoundersAsync(
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken)
    {
        var founderIds = StonehavenFounderIds.ToHashSet();
        var settlement = await database.Settlements
            .Include(x => x.Residents)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        foreach (var resident in settlement.Residents)
        {
            if (founderIds.Contains(resident.Id))
            {
                resident.Health = resident.MaximumHealth;
                resident.Status = ResidentStatus.Active;
            }
            else if (resident.Status != ResidentStatus.Dead)
            {
                resident.Status = ResidentStatus.Missing;
            }
            resident.UpdatedAt = restoredAt;
        }

        settlement.Population = WorldPopulationService.StartingStonehavenPopulation;
        settlement.StructuralIntegrity = 100;
        settlement.Food = Math.Max(settlement.Food, WorldPopulationService.StartingStonehavenFood);
        settlement.Wood = Math.Max(settlement.Wood, WorldPopulationService.StartingStonehavenWood);
        settlement.Stone = Math.Max(settlement.Stone, WorldPopulationService.StartingStonehavenStone);
        settlement.Iron = Math.Max(settlement.Iron, WorldPopulationService.StartingStonehavenIron);
        settlement.DefenseRating = 12;
        settlement.GuardStrength = 10;
        settlement.IsDestroyed = false;
        settlement.UpdatedAt = restoredAt;
    }

    private async Task RestoreDarkwoodFoundersAsync(
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken)
    {
        var founderIds = DarkwoodFounderIds.ToHashSet();
        var faction = await database.Factions
            .Include(x => x.Resources)
            .Include(x => x.Structures)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var creatures = await database.Creatures
            .Include(x => x.Species)
            .Where(x => x.FactionId == faction.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var creature in creatures)
        {
            if (founderIds.Contains(creature.Id))
            {
                creature.Status = CreatureStatus.Alive;
                creature.Health = creature.MaximumHealth;
                creature.Role = creature.Id == LivingRealmsDbContext.GoblinChiefCreatureId
                    ? "Chief"
                    : creature.Title ?? creature.Role;
                creature.PositionX = creature.SpawnX;
                creature.PositionY = creature.SpawnY;
                creature.PositionZ = creature.SpawnZ;
                creature.RespawnAt = null;
            }
            else if (creature.Role != "Raid Attacker")
            {
                creature.Status = CreatureStatus.Retired;
                creature.Health = 0;
                creature.RespawnAt = null;
            }
            creature.UpdatedAt = restoredAt;
            creature.LastProcessedAt = restoredAt;
        }

        faction.Population = WorldPopulationService.StartingDarkwoodPopulation;
        faction.PopulationCapacity = 10;
        faction.DevelopmentStage = 1;
        faction.TerritorySize = 1;
        faction.TechnologyLevel = 1;
        faction.MilitaryStrength = 66;
        faction.Morale = 55;
        faction.Aggression = 45;
        faction.LeaderCreatureId = LivingRealmsDbContext.GoblinChiefCreatureId;
        faction.UpdatedAt = restoredAt;
        SetMinimumResource(faction.Resources, ResourceKind.Food, 80, restoredAt);
        SetMinimumResource(faction.Resources, ResourceKind.Wood, 50, restoredAt);
        SetMinimumResource(faction.Resources, ResourceKind.Stone, 15, restoredAt);
        SetMinimumResource(faction.Resources, ResourceKind.Iron, 5, restoredAt);

        var advancedStructures = faction.Structures
            .Where(x => x.StructureType is not "Hide Tents" and not "Crude Stockpile")
            .ToArray();
        database.FactionStructures.RemoveRange(advancedStructures);
        foreach (var structure in faction.Structures.Except(advancedStructures))
        {
            structure.Level = 1;
            structure.Health = 100;
            structure.UpdatedAt = restoredAt;
        }

        var palisade = await database.ConstructionProjects
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId, cancellationToken);
        palisade.CurrentLevel = 0;
        palisade.WoodContributed = 0;
        palisade.StoneContributed = 0;
        palisade.CompletedAt = null;
        palisade.LastNpcContributionAt = null;
        palisade.UpdatedAt = restoredAt;
    }

    private async Task AdvanceRebuildingAsync(
        SettlementRecovery recovery,
        DateTimeOffset advancedAt,
        int worldHours,
        CancellationToken cancellationToken)
    {
        var developmentStage = await database.Factions
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.DevelopmentStage)
            .SingleAsync(cancellationToken);
        var structures = await database.WorldStructures
            .Include(x => x.ConstructionProject)
            .Where(x => x.Owner == recovery.Owner)
            .ToArrayAsync(cancellationToken);
        var built = structures
            .Where(x => WorldStructureService.IsBuilt(x, developmentStage))
            .ToArray();
        var repairBudget = Math.Max(1, worldHours) *
                           (recovery.Owner == ResourceOwner.Stonehaven
                               ? StonehavenRepairPointsPerWorldHour
                               : DarkwoodRepairPointsPerWorldHour);

        while (repairBudget > 0)
        {
            var target = built
                .Where(x => x.Health < x.MaximumHealth)
                .OrderBy(RecoveryPriority)
                .ThenBy(x => x.DisplayOrder)
                .FirstOrDefault();
            if (target is null)
            {
                var defenseProjectId = recovery.Owner == ResourceOwner.Stonehaven
                    ? LivingRealmsDbContext.StonehavenWallProjectId
                    : LivingRealmsDbContext.DarkwoodPalisadeProjectId;
                var defenseProject = await database.ConstructionProjects
                    .SingleAsync(x => x.Id == defenseProjectId, cancellationToken);
                if (defenseProject.CurrentLevel < 1)
                {
                    recovery.CurrentStructureKey = defenseProject.Key;
                    recovery.LastProgressedAt = advancedAt;
                    recovery.UpdatedAt = advancedAt;
                    break;
                }

                built = structures
                    .Where(x => WorldStructureService.IsBuilt(x, developmentStage))
                    .ToArray();
                target = built
                    .Where(x => x.Health < x.MaximumHealth)
                    .OrderBy(RecoveryPriority)
                    .ThenBy(x => x.DisplayOrder)
                    .FirstOrDefault();
                if (target is null)
                {
                    CompleteRecovery(recovery, structures, developmentStage, advancedAt);
                    break;
                }
            }

            recovery.CurrentStructureKey = target.Key;
            var requestedRepair = Math.Min(repairBudget, target.MaximumHealth - target.Health);
            var affordableRepair = await ConsumeRepairMaterialsAsync(
                recovery.Owner,
                target.Kind,
                requestedRepair,
                cancellationToken);
            if (affordableRepair <= 0)
            {
                break;
            }

            target.Health += affordableRepair;
            target.DestroyedAt = null;
            target.UpdatedAt = advancedAt;
            repairBudget -= affordableRepair;
            recovery.RebuildCycles++;
            recovery.LastProgressedAt = advancedAt;
            recovery.UpdatedAt = advancedAt;
        }

        if (recovery.Status == SettlementRecoveryStatus.Rebuilding &&
            built.All(x => x.Health >= x.MaximumHealth))
        {
            var defenseProjectId = recovery.Owner == ResourceOwner.Stonehaven
                ? LivingRealmsDbContext.StonehavenWallProjectId
                : LivingRealmsDbContext.DarkwoodPalisadeProjectId;
            var defenseProjectReady = await database.ConstructionProjects
                .AnyAsync(x => x.Id == defenseProjectId && x.CurrentLevel >= 1, cancellationToken);
            if (defenseProjectReady)
            {
                CompleteRecovery(recovery, structures, developmentStage, advancedAt);
            }
        }
    }

    private async Task<int> ConsumeRepairMaterialsAsync(
        ResourceOwner owner,
        WorldStructureKind kind,
        int requestedRepair,
        CancellationToken cancellationToken)
    {
        var requestedUnits = Math.Max(1, (int)Math.Ceiling(requestedRepair / 100d));
        var (woodPerUnit, stonePerUnit) = RepairMaterialCost(kind);
        if (owner == ResourceOwner.Stonehaven)
        {
            var settlement = await database.Settlements
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
            var affordableUnits = AffordableUnits(
                requestedUnits,
                settlement.Wood,
                settlement.Stone,
                woodPerUnit,
                stonePerUnit);
            if (affordableUnits == 0)
            {
                return 0;
            }
            settlement.Wood -= affordableUnits * woodPerUnit;
            settlement.Stone -= affordableUnits * stonePerUnit;
            return Math.Min(requestedRepair, affordableUnits * 100);
        }

        var resources = await database.FactionResources
            .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                        (x.Kind == ResourceKind.Wood || x.Kind == ResourceKind.Stone))
            .ToDictionaryAsync(x => x.Kind, cancellationToken);
        var affordable = AffordableUnits(
            requestedUnits,
            resources[ResourceKind.Wood].Amount,
            resources[ResourceKind.Stone].Amount,
            woodPerUnit,
            stonePerUnit);
        if (affordable == 0)
        {
            return 0;
        }
        resources[ResourceKind.Wood].Amount -= affordable * woodPerUnit;
        resources[ResourceKind.Stone].Amount -= affordable * stonePerUnit;
        return Math.Min(requestedRepair, affordable * 100);
    }

    private void CompleteRecovery(
        SettlementRecovery recovery,
        IEnumerable<WorldStructure> structures,
        int developmentStage,
        DateTimeOffset recoveredAt)
    {
        foreach (var futureStructure in structures.Where(x =>
                     !WorldStructureService.IsBuilt(x, developmentStage)))
        {
            futureStructure.Health = futureStructure.MaximumHealth;
            futureStructure.LastDamagedAt = null;
            futureStructure.DestroyedAt = null;
            futureStructure.UpdatedAt = recoveredAt;
        }
        recovery.Status = SettlementRecoveryStatus.Healthy;
        recovery.RecoveredAt = recoveredAt;
        recovery.LastProgressedAt = recoveredAt;
        recovery.CurrentStructureKey = null;
        recovery.UpdatedAt = recoveredAt;
        if (recovery.Owner == ResourceOwner.Stonehaven)
        {
            var settlement = database.Settlements.Local.SingleOrDefault(x =>
                x.Id == LivingRealmsDbContext.StonehavenVillageId);
            if (settlement is not null)
            {
                settlement.StructuralIntegrity = Math.Max(1000, settlement.StructuralIntegrity);
                settlement.DefenseRating = Math.Max(65, settlement.DefenseRating);
                settlement.GuardStrength = Math.Max(42, settlement.GuardStrength);
                settlement.UpdatedAt = recoveredAt;
            }
        }
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "settlement_recovered",
            Title = $"{OwnerName(recovery.Owner)} completed its recovery",
            Description =
                $"{OwnerName(recovery.Owner)} restored every currently built functional structure before its gates and walls. " +
                $"Natural population growth can continue from the {recovery.FoundingPopulation} returning founders.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = recovery.Owner == ResourceOwner.Darkwood
                ? LivingRealmsDbContext.DarkwoodClanId
                : null,
            OccurredAt = recoveredAt,
            ImportanceLevel = 5,
            CreatedAt = recoveredAt,
            UpdatedAt = recoveredAt
        });
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogRecoveryCompleted(logger, recovery.Owner, recoveredAt);
        }
    }

    private async Task<IReadOnlyList<SettlementRecoveryState>> BuildStatesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var developmentStage = await database.Factions.AsNoTracking()
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.DevelopmentStage)
            .SingleAsync(cancellationToken);
        var recoveries = await database.SettlementRecoveries.AsNoTracking()
            .OrderBy(x => x.Owner)
            .ToArrayAsync(cancellationToken);
        var structures = await database.WorldStructures.AsNoTracking()
            .Include(x => x.ConstructionProject)
            .ToArrayAsync(cancellationToken);
        return recoveries.Select(recovery =>
        {
            var built = structures
                .Where(x => x.Owner == recovery.Owner &&
                            WorldStructureService.IsBuilt(x, developmentStage))
                .ToArray();
            var functional = built.Where(x => !IsDefensive(x.Kind)).ToArray();
            var defenses = built.Where(x => IsDefensive(x.Kind)).ToArray();
            var secondsRemaining = recovery.Status == SettlementRecoveryStatus.Defeated &&
                                   recovery.RecoveryEligibleAt is not null
                ? Math.Max(0, (int)Math.Ceiling((recovery.RecoveryEligibleAt.Value - now).TotalSeconds))
                : 0;
            return new SettlementRecoveryState(
                recovery.Owner.ToString(),
                OwnerName(recovery.Owner),
                recovery.Status.ToString(),
                recovery.FoundingPopulation,
                recovery.DefeatedAt,
                recovery.RecoveryEligibleAt,
                secondsRemaining,
                recovery.RebuildingStartedAt,
                recovery.LastProgressedAt,
                recovery.RecoveredAt,
                recovery.CurrentStructureKey,
                recovery.RebuildCycles,
                functional.Count(x => x.Health >= x.MaximumHealth),
                functional.Length,
                defenses.Count(x => x.Health >= x.MaximumHealth),
                defenses.Length,
                built.Sum(x => x.Health),
                built.Sum(x => x.MaximumHealth));
        }).ToArray();
    }

    private static (int Wood, int Stone) RepairMaterialCost(WorldStructureKind kind) => kind switch
    {
        WorldStructureKind.Wall or WorldStructureKind.Gate => (2, 2),
        WorldStructureKind.Farm => (1, 0),
        WorldStructureKind.Dock => (2, 0),
        WorldStructureKind.Mine => (1, 2),
        WorldStructureKind.Stockpile => (2, 1),
        _ => (2, 1)
    };

    private static int AffordableUnits(
        int requested,
        long wood,
        long stone,
        int woodPerUnit,
        int stonePerUnit)
    {
        var woodUnits = woodPerUnit == 0 ? requested : (int)Math.Min(requested, wood / woodPerUnit);
        var stoneUnits = stonePerUnit == 0 ? requested : (int)Math.Min(requested, stone / stonePerUnit);
        return Math.Max(0, Math.Min(requested, Math.Min(woodUnits, stoneUnits)));
    }

    private static int RecoveryPriority(WorldStructure structure) => structure.Kind switch
    {
        WorldStructureKind.Stockpile => 0,
        WorldStructureKind.Farm => 1,
        WorldStructureKind.Building => 2,
        WorldStructureKind.Mine => 3,
        WorldStructureKind.Dock => 4,
        WorldStructureKind.Gate => 10,
        WorldStructureKind.Wall => 11,
        _ => 5
    };

    private static bool IsDefensive(WorldStructureKind kind) =>
        kind is WorldStructureKind.Wall or WorldStructureKind.Gate;

    private static string OwnerName(ResourceOwner owner) =>
        owner == ResourceOwner.Stonehaven ? "Stonehaven" : "Darkwood";

    private static void SetMinimumResource(
        IEnumerable<FactionResource> resources,
        ResourceKind kind,
        long minimum,
        DateTimeOffset updatedAt)
    {
        var resource = resources.Single(x => x.Kind == kind);
        resource.Amount = Math.Max(resource.Amount, minimum);
        resource.UpdatedAt = updatedAt;
    }

    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Warning,
        Message = "{Owner} entered the fifteen-minute defeat period at {DefeatedAt}")]
    private static partial void LogSettlementDefeated(
        ILogger logger,
        ResourceOwner owner,
        DateTimeOffset defeatedAt);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "{Owner} founding population returned and began rebuilding at {StartedAt}")]
    private static partial void LogRebuildingStarted(
        ILogger logger,
        ResourceOwner owner,
        DateTimeOffset startedAt);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "{Owner} completed recovery at {RecoveredAt}")]
    private static partial void LogRecoveryCompleted(
        ILogger logger,
        ResourceOwner owner,
        DateTimeOffset recoveredAt);
}

public sealed record SettlementRecoveryState(
    string Owner,
    string Name,
    string Status,
    int FoundingPopulation,
    DateTimeOffset? DefeatedAt,
    DateTimeOffset? RecoveryEligibleAt,
    int RecoverySecondsRemaining,
    DateTimeOffset? RebuildingStartedAt,
    DateTimeOffset? LastProgressedAt,
    DateTimeOffset? RecoveredAt,
    string? CurrentStructureKey,
    int RebuildCycles,
    int FunctionalStructuresRestored,
    int FunctionalStructuresTotal,
    int DefensesRestored,
    int DefensesTotal,
    int StructureHealth,
    int StructureMaximumHealth);
