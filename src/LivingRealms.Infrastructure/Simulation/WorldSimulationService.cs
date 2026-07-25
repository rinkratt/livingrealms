using System.Data;
using System.Text.Json;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LivingRealms.Infrastructure.Simulation;

public sealed partial class WorldSimulationService(
    LivingRealmsDbContext database,
    RaidSimulationService raidSimulation,
    WorldPopulationService population,
    FactionLeadershipService leadership,
    IOptions<WorldSimulationOptions> options,
    ILogger<WorldSimulationService> logger)
{
    public const string ProgressionEventType = "WorldProgression";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorldSimulationRunResult> ProcessOfflineProgressionAsync(
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        var recovered = await RecoverInterruptedEventsAsync(processedAt, cancellationToken);
        var faction = await database.Factions.AsNoTracking()
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var elapsedMinutes = Math.Max(0, (processedAt - faction.LastProcessedAt).TotalMinutes);
        var elapsedWorldMinutes = elapsedMinutes * Math.Max(0.01, options.Value.WorldMinutesPerRealMinute);
        var worldHours = Math.Min(
            options.Value.MaximumCatchUpWorldHours,
            (int)Math.Floor(elapsedWorldMinutes / 60.0));

        if (worldHours > 0)
        {
            var key = $"offline-progress:{faction.Id:N}:{faction.LastProcessedAt.UtcTicks}:{worldHours}";
            await QueueProgressionEventAsync(worldHours, processedAt, "offline-worker", key, cancellationToken);
        }

        var processed = await ProcessDueEventsAsync(processedAt, cancellationToken);
        return new WorldSimulationRunResult(processed, recovered, worldHours);
    }

    public async Task<WorldSimulationRunResult> AdvanceForTestingAsync(
        int worldHours,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        if (worldHours is < 1 or > 168)
        {
            throw new ArgumentOutOfRangeException(nameof(worldHours), "World advancement must be between 1 and 168 hours.");
        }

        var recovered = await RecoverInterruptedEventsAsync(processedAt, cancellationToken);
        await QueueProgressionEventAsync(
            worldHours,
            processedAt,
            "development-control",
            $"development-progress:{Guid.NewGuid():N}",
            cancellationToken);
        var processed = await ProcessDueEventsAsync(processedAt, cancellationToken);
        return new WorldSimulationRunResult(processed, recovered, worldHours);
    }

    public async Task ResetForTestingAsync(
        DateTimeOffset resetAt,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (database.Database.IsRelational())
            {
                transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            var faction = await database.Factions
                .Include(x => x.Resources)
                .Include(x => x.Structures)
                .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
            var raids = await database.SettlementRaids
                .Include(x => x.Attackers)
                .ThenInclude(x => x.Creature)
                .ToListAsync(cancellationToken);
            var assaults = await database.StonehavenAssaults
                .Include(x => x.Members)
                .ToListAsync(cancellationToken);
            var worldCreatures = await database.Creatures
                .Where(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId)
                .ToListAsync(cancellationToken);
            var leader = worldCreatures.Single(x => x.Id == LivingRealmsDbContext.GoblinChiefCreatureId);

            faction.Population = WorldPopulationService.StartingDarkwoodPopulation;
            faction.PopulationCapacity = 10;
            faction.DevelopmentStage = 1;
            faction.TerritorySize = 1;
            faction.Aggression = 45;
            faction.Morale = 55;
            faction.TechnologyLevel = 1;
            faction.MilitaryStrength = 66;
            faction.LeaderCreatureId = LivingRealmsDbContext.GoblinChiefCreatureId;
            faction.SimulatedHours = 0;
            faction.LastProcessedAt = resetAt;
            faction.NextDecisionAt = resetAt.AddHours(1);
            faction.UpdatedAt = resetAt;

            ResetResource(faction.Resources, ResourceKind.Food, 80, 250, resetAt);
            ResetResource(faction.Resources, ResourceKind.Wood, 50, 250, resetAt);
            ResetResource(faction.Resources, ResourceKind.Stone, 15, 180, resetAt);
            ResetResource(faction.Resources, ResourceKind.Iron, 5, 120, resetAt);
            ResetResource(faction.Resources, ResourceKind.Gold, 0, 100, resetAt);

            var advancedStructures = faction.Structures
                .Where(x => x.StructureType is not "Hide Tents" and not "Crude Stockpile")
                .ToArray();
            database.FactionStructures.RemoveRange(advancedStructures);
            foreach (var structure in faction.Structures.Except(advancedStructures))
            {
                structure.Level = 1;
                structure.Health = 100;
                structure.UpdatedAt = resetAt;
            }

            leader.Level = 8;
            leader.Experience = 0;
            leader.MaximumHealth = 180;
            leader.Health = 180;
            leader.Attack = 22;
            leader.Defense = 14;
            leader.Leadership = 10;
            leader.Title = "Goblin Chief";
            foreach (var attacker in raids.SelectMany(x => x.Attackers))
            {
                attacker.Creature.Role = attacker.Creature.Title ?? "Clan Raider";
            }
            foreach (var creature in worldCreatures)
            {
                creature.Status = CreatureStatus.Alive;
                creature.Health = creature.MaximumHealth;
                creature.RespawnAt = null;
                creature.LastAttackAt = null;
                creature.PositionX = creature.SpawnX;
                creature.PositionY = creature.SpawnY;
                creature.PositionZ = creature.SpawnZ;
                creature.LastProcessedAt = resetAt;
                creature.UpdatedAt = resetAt;
            }

            if (raids.Count > 0)
            {
                database.SettlementRaids.RemoveRange(raids);
            }

            var permanentDarkwoodIds = new HashSet<Guid>
            {
                LivingRealmsDbContext.GoblinChiefCreatureId,
                Guid.Parse("9230414d-a60d-46ca-9c59-36cc3b867201"),
                Guid.Parse("9230414d-a60d-46ca-9c59-36cc3b867202")
            };
            permanentDarkwoodIds.UnionWith(worldCreatures
                .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                            x.Id.ToString().StartsWith("74000000", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .Take(4)
                .Select(x => x.Id));
            var excessDarkwoodMembers = worldCreatures
                .Where(x => x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                            !permanentDarkwoodIds.Contains(x.Id))
                .ToArray();
            if (excessDarkwoodMembers.Length > 0)
            {
                database.Creatures.RemoveRange(excessDarkwoodMembers);
            }

            var settlement = await database.Settlements
                .Include(x => x.Residents)
                .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
            settlement.Population = WorldPopulationService.StartingStonehavenPopulation;
            settlement.StructuralIntegrity = 1000;
            settlement.Food = WorldPopulationService.StartingStonehavenFood;
            settlement.Wood = WorldPopulationService.StartingStonehavenWood;
            settlement.Stone = WorldPopulationService.StartingStonehavenStone;
            settlement.Iron = WorldPopulationService.StartingStonehavenIron;
            settlement.DefenseRating = 65;
            settlement.GuardStrength = 42;
            settlement.LastAttackedAt = null;
            settlement.IsDestroyed = false;
            settlement.UpdatedAt = resetAt;
            var originalStonehavenIds = new HashSet<Guid>
            {
                LivingRealmsDbContext.StonehavenLeaderResidentId,
                LivingRealmsDbContext.MiraResidentId,
                LivingRealmsDbContext.TomasResidentId,
                LivingRealmsDbContext.BrannResidentId,
                LivingRealmsDbContext.MaraVennResidentId,
                LivingRealmsDbContext.ElowenResidentId,
                LivingRealmsDbContext.OrenResidentId,
                LivingRealmsDbContext.NessaResidentId,
                LivingRealmsDbContext.DainResidentId
            };
            var laterResidents = settlement.Residents
                .Where(x => !originalStonehavenIds.Contains(x.Id))
                .ToArray();
            if (laterResidents.Length > 0)
            {
                database.SettlementResidents.RemoveRange(laterResidents);
            }
            foreach (var resident in settlement.Residents.Except(laterResidents))
            {
                resident.Health = resident.MaximumHealth;
                resident.Status = resident.Id == LivingRealmsDbContext.MaraVennResidentId
                    ? ResidentStatus.Missing
                    : ResidentStatus.Active;
                resident.UpdatedAt = resetAt;
            }

            if (assaults.Count > 0)
            {
                database.StonehavenAssaults.RemoveRange(assaults);
            }

            var constructionProjects = await database.ConstructionProjects.ToListAsync(cancellationToken);
            foreach (var project in constructionProjects)
            {
                project.CurrentLevel = 0;
                project.MaximumLevel = 3;
                project.WoodContributed = 0;
                project.StoneContributed = 0;
                project.CompletedAt = null;
                project.LastNpcContributionAt = null;
                (project.WoodRequired, project.StoneRequired) = project.Key switch
                {
                    "stonehaven-curtain-wall" => (240, 300),
                    "darkwood-perimeter-palisade" => (320, 80),
                    "stonehaven-lumber-yard" => (120, 40),
                    "stonehaven-quarry-works" => (70, 150),
                    "darkwood-supply-hut" => (100, 30),
                    _ => (project.WoodRequired, project.StoneRequired)
                };
                project.UpdatedAt = resetAt;
            }
            var resourceNodes = await database.WorldResourceNodes.ToListAsync(cancellationToken);
            foreach (var node in resourceNodes)
            {
                node.Remaining = node.Capacity;
                node.RespawnAt = null;
                node.UpdatedAt = resetAt;
            }
            database.ResourceContributions.RemoveRange(
                await database.ResourceContributions.ToListAsync(cancellationToken));

            database.ScheduledEvents.RemoveRange(await database.ScheduledEvents
                .Where(x => x.TargetId == faction.Id || x.EventType == ProgressionEventType)
                .ToListAsync(cancellationToken));
            database.WorldHistory.RemoveRange(await database.WorldHistory
                .Where(x => x.FactionId == faction.Id ||
                            x.EventType == "construction_completed" ||
                            x.EventType == "construction_upgraded")
                .ToListAsync(cancellationToken));
            AddHistory(
                "playtest_reset",
                "The Stonehaven Valley chronicle began anew",
                $"The valley returned to {WorldPopulationService.StartingStonehavenPopulation} active Stonehaven residents and {WorldPopulationService.StartingDarkwoodPopulation} Darkwood goblins. Player characters and their progress were not changed.",
                3,
                faction,
                leader,
                resetAt);

            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            LogWorldReset(logger, CentralNow());
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> QueueProgressionEventAsync(
        int worldHours,
        DateTimeOffset processedAt,
        string source,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (await database.ScheduledEvents.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return false;
        }

        database.ScheduledEvents.Add(new ScheduledEvent
        {
            EventType = ProgressionEventType,
            TargetId = LivingRealmsDbContext.DarkwoodClanId,
            ScheduledAt = processedAt,
            Status = ScheduledEventStatus.Pending,
            IdempotencyKey = idempotencyKey,
            PayloadJson = JsonSerializer.Serialize(
                new ProgressionPayload(worldHours, processedAt, source),
                JsonOptions),
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (await database.ScheduledEvents.AsNoTracking()
                    .AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    private async Task<int> ProcessDueEventsAsync(
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var processedCount = 0;
        for (var eventNumber = 0; eventNumber < options.Value.MaximumEventsPerRun; eventNumber++)
        {
            var eventId = await ClaimNextEventAsync(processedAt, cancellationToken);
            if (eventId is null)
            {
                break;
            }

            if (await ProcessClaimedEventAsync(eventId.Value, cancellationToken))
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    private async Task<Guid?> ClaimNextEventAsync(
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var candidateId = await database.ScheduledEvents.AsNoTracking()
            .Where(x => x.EventType == ProgressionEventType &&
                        x.Status == ScheduledEventStatus.Pending &&
                        x.ScheduledAt <= processedAt)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidateId is null)
        {
            return null;
        }

        if (!database.Database.IsRelational())
        {
            var inMemoryEvent = await database.ScheduledEvents.FindAsync([candidateId.Value], cancellationToken);
            if (inMemoryEvent is null || inMemoryEvent.Status != ScheduledEventStatus.Pending)
            {
                return null;
            }

            inMemoryEvent.Status = ScheduledEventStatus.Processing;
            inMemoryEvent.StartedAt = processedAt;
            inMemoryEvent.UpdatedAt = processedAt;
            await database.SaveChangesAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return candidateId;
        }

        var affected = await database.ScheduledEvents
            .Where(x => x.Id == candidateId.Value && x.Status == ScheduledEventStatus.Pending)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.Status, ScheduledEventStatus.Processing)
                .SetProperty(x => x.StartedAt, processedAt)
                .SetProperty(x => x.UpdatedAt, processedAt), cancellationToken);
        database.ChangeTracker.Clear();
        return affected == 1 ? candidateId : null;
    }

    private async Task<bool> ProcessClaimedEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (database.Database.IsRelational())
            {
                transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            var scheduledEvent = await database.ScheduledEvents
                .SingleAsync(x => x.Id == eventId, cancellationToken);
            if (scheduledEvent.Status != ScheduledEventStatus.Processing)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return false;
            }

            var payload = JsonSerializer.Deserialize<ProgressionPayload>(scheduledEvent.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("The world-progression event payload was empty.");
            await ApplyProgressionAsync(payload, cancellationToken);

            scheduledEvent.Status = ScheduledEventStatus.Completed;
            scheduledEvent.CompletedAt = payload.ProcessedAt;
            scheduledEvent.ErrorMessage = null;
            scheduledEvent.UpdatedAt = payload.ProcessedAt;
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var centralTime = CentralNow();
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogProgressionProcessed(
                    logger,
                    payload.WorldHours,
                    payload.Source,
                    centralTime);
            }
            return true;
        }
        catch (Exception exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            database.ChangeTracker.Clear();
            var diagnosticMessage = exception is DbUpdateConcurrencyException concurrencyException
                ? $"{exception.Message} Entries: {string.Join(", ", concurrencyException.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"))}"
                : exception.Message;
            var failedEvent = await database.ScheduledEvents.SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);
            if (failedEvent is not null)
            {
                failedEvent.RetryCount++;
                failedEvent.Status = failedEvent.RetryCount >= options.Value.MaximumEventRetries
                    ? ScheduledEventStatus.Failed
                    : ScheduledEventStatus.Pending;
                failedEvent.ErrorMessage = diagnosticMessage.Length > 500
                    ? diagnosticMessage[..500]
                    : diagnosticMessage;
                failedEvent.UpdatedAt = DateTimeOffset.UtcNow;
                await database.SaveChangesAsync(cancellationToken);
            }

            var loggedException = diagnosticMessage == exception.Message
                ? exception
                : new InvalidOperationException(diagnosticMessage, exception);
            LogProgressionFailed(logger, loggedException, eventId, CentralNow());
            return false;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task ApplyProgressionAsync(ProgressionPayload payload, CancellationToken cancellationToken)
    {
        var faction = await database.Factions
            .Include(x => x.Resources)
            .Include(x => x.Structures)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var settlement = await database.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        var leader = await leadership.EnsureLeaderAsync(faction, payload.ProcessedAt, cancellationToken)
            ?? throw new InvalidOperationException("Darkwood has no living candidate able to lead the faction.");
        var creatures = await database.Creatures
            .Include(x => x.Species)
            .Where(x => x.RegionId == LivingRealmsDbContext.StonehavenValleyId)
            .ToListAsync(cancellationToken);
        var activeStonehavenResidents = await database.SettlementResidents
            .Where(x => x.SettlementId == settlement.Id &&
                        x.Health > 0 &&
                        (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured))
            .ToListAsync(cancellationToken);

        var resources = faction.Resources.ToDictionary(x => x.Kind);
        var foodBefore = resources[ResourceKind.Food].Amount;
        var woodBefore = resources[ResourceKind.Wood].Amount;
        var stoneBefore = resources[ResourceKind.Stone].Amount;
        var ironBefore = resources[ResourceKind.Iron].Amount;
        var populationBefore = faction.Population;
        var stonehavenPopulationBefore = settlement.Population;
        var stageBefore = faction.DevelopmentStage;
        var titleBefore = leader.Title;
        var simulatedHoursBefore = faction.SimulatedHours;

        AddResource(resources[ResourceKind.Food], (long)faction.Population * payload.WorldHours);
        AddResource(resources[ResourceKind.Wood], 5L * payload.WorldHours);
        AddResource(resources[ResourceKind.Stone], 2L * payload.WorldHours);
        AddResource(resources[ResourceKind.Iron], payload.WorldHours);
        AddResource(resources[ResourceKind.Gold], payload.WorldHours / 8L);

        faction.SimulatedHours += payload.WorldHours;
        var previousGrowthCycles = simulatedHoursBefore / 12;
        var currentGrowthCycles = faction.SimulatedHours / 12;
        var possibleGrowth = (int)Math.Min(24, currentGrowthCycles - previousGrowthCycles);
        for (var cycle = 0; cycle < possibleGrowth; cycle++)
        {
            if (faction.Population >= faction.PopulationCapacity || resources[ResourceKind.Food].Amount < 15)
            {
                break;
            }

            faction.Population++;
            resources[ResourceKind.Food].Amount -= 15;
        }

        var farmers = activeStonehavenResidents.Count(x => x.Role == "Farmer");
        var huntersAndFishers = activeStonehavenResidents.Count(x => x.Role is "Hunter" or "Fisher");
        var lumberjacks = activeStonehavenResidents.Count(x => x.Role == "Lumberjack");
        var quarryWorkers = activeStonehavenResidents.Count(x => x.Role is "Quarry Worker" or "Mason");
        var ironWorkers = activeStonehavenResidents.Count(x => x.Role is "Quarry Worker" or "Blacksmith");
        var foodProducedPerHour = WorldPopulationService.StonehavenFarmPlotCount / 2 +
                                  farmers * 2 + huntersAndFishers;
        var foodConsumedPerHour = Math.Max(1, (settlement.Population + 3) / 4);
        settlement.Food = Math.Max(0,
            settlement.Food + (foodProducedPerHour - foodConsumedPerHour) * payload.WorldHours);
        settlement.Wood = Math.Min(700,
            settlement.Wood + Math.Max(0, lumberjacks) * payload.WorldHours);
        settlement.Stone = Math.Min(700,
            settlement.Stone + Math.Max(0, quarryWorkers) * payload.WorldHours);
        var previousIronCycles = simulatedHoursBefore / 4;
        var currentIronCycles = faction.SimulatedHours / 4;
        settlement.Iron = Math.Min(350,
            settlement.Iron + (int)Math.Max(0, currentIronCycles - previousIronCycles) * ironWorkers);

        var previousStonehavenGrowthCycles = simulatedHoursBefore / 24;
        var currentStonehavenGrowthCycles = faction.SimulatedHours / 24;
        var possibleStonehavenGrowth = (int)Math.Min(7,
            currentStonehavenGrowthCycles - previousStonehavenGrowthCycles);
        for (var cycle = 0; cycle < possibleStonehavenGrowth; cycle++)
        {
            var nextPopulation = settlement.Population + 1;
            var foodReserve = nextPopulation * 6;
            const int arrivalFoodCost = 32;
            const int arrivalWoodCost = 20;
            const int arrivalStoneCost = 12;
            const int arrivalIronCost = 2;
            if (settlement.IsDestroyed ||
                settlement.Population >= WorldPopulationService.StonehavenHousingCapacity ||
                settlement.Food < foodReserve + arrivalFoodCost ||
                settlement.Wood < arrivalWoodCost ||
                settlement.Stone < arrivalStoneCost ||
                settlement.Iron < arrivalIronCost)
            {
                break;
            }

            settlement.Population++;
            settlement.Food -= arrivalFoodCost;
            settlement.Wood -= arrivalWoodCost;
            settlement.Stone -= arrivalStoneCost;
            settlement.Iron -= arrivalIronCost;
        }
        settlement.GuardStrength = Math.Max(
            settlement.GuardStrength,
            42 + Math.Max(0, settlement.Population - WorldPopulationService.StartingStonehavenPopulation) * 3);
        settlement.DefenseRating = Math.Max(
            settlement.DefenseRating,
            65 + Math.Max(0, settlement.Population - WorldPopulationService.StartingStonehavenPopulation) / 4);
        if (settlement.Population > stonehavenPopulationBefore)
        {
            await population.EnsureStonehavenResidentsAsync(saveChanges: false, cancellationToken);
        }

        var constructionProjects = await database.ConstructionProjects
            .Where(x => x.Id == LivingRealmsDbContext.StonehavenWallProjectId ||
                        x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var stonehavenWall = constructionProjects[LivingRealmsDbContext.StonehavenWallProjectId];
        var darkwoodPalisade = constructionProjects[LivingRealmsDbContext.DarkwoodPalisadeProjectId];
        AdvanceSettlementConstruction(
            payload.WorldHours,
            payload.ProcessedAt,
            faction,
            settlement,
            activeStonehavenResidents,
            creatures,
            resources,
            stonehavenWall,
            darkwoodPalisade);

        if (faction.DevelopmentStage == 1 &&
            faction.Population >= 8 &&
            resources[ResourceKind.Food].Amount >= 100 &&
            (darkwoodPalisade.CurrentLevel >= 1 ||
             darkwoodPalisade.WoodContributed >= 120 &&
             darkwoodPalisade.StoneContributed >= 40))
        {
            resources[ResourceKind.Food].Amount -= 20;
            faction.DevelopmentStage = 2;
            faction.PopulationCapacity = 16;
            faction.TechnologyLevel = 2;
            faction.Morale = Math.Min(100, faction.Morale + 8);
            ExpandCapacities(resources, 400, 400, 300, 200, 150);
            AddStructure(faction, "Timber Palisade", payload.ProcessedAt);
            AddStructure(faction, "Hunter Lodge", payload.ProcessedAt);
        }

        if (faction.DevelopmentStage == 2 &&
            faction.SimulatedHours >= 72 &&
            faction.Population >= 14 &&
            resources[ResourceKind.Iron].Amount >= 30 &&
            darkwoodPalisade.CurrentLevel >= darkwoodPalisade.MaximumLevel)
        {
            resources[ResourceKind.Iron].Amount -= 30;
            faction.DevelopmentStage = 3;
            faction.PopulationCapacity = 24;
            faction.TechnologyLevel = 3;
            faction.Morale = Math.Min(100, faction.Morale + 10);
            ExpandCapacities(resources, 700, 700, 500, 350, 250);
            AddStructure(faction, "Darkwood Watchtower", payload.ProcessedAt);
            AddStructure(faction, "Iron Workshop", payload.ProcessedAt);
        }

        leader.Experience += 12L * payload.WorldHours;
        var leaderLeveled = false;
        while (leader.Experience >= leader.Level * 30L && leader.Level < 30)
        {
            leader.Experience -= leader.Level * 30L;
            leader.Level++;
            leader.Leadership += 3;
            leaderLeveled = true;
        }

        // The leader's simulated level is also his authoritative combat tier.
        var previousMaximumHealth = leader.MaximumHealth;
        var levelsBeyondChief = Math.Max(0, leader.Level - 8);
        leader.MaximumHealth = 180 + levelsBeyondChief * 24;
        leader.Attack = 22 + levelsBeyondChief * 3;
        leader.Defense = 14 + levelsBeyondChief * 2;
        if (leader.MaximumHealth > previousMaximumHealth)
        {
            leader.Health = Math.Min(
                leader.MaximumHealth,
                leader.Health + leader.MaximumHealth - previousMaximumHealth);
        }

        leader.Title = faction.DevelopmentStage >= 3 && leader.Level >= 12
            ? "Goblin Warlord"
            : faction.DevelopmentStage >= 2 && leader.Level >= 9
                ? "Goblin Chieftain"
                : "Goblin Chief";
        faction.LeaderCreatureId = leader.Id;
        faction.MilitaryStrength = faction.Population * 6 + leader.Level * 8 +
                                   faction.DevelopmentStage * 25 + darkwoodPalisade.CurrentLevel * 14;
        faction.TerritorySize = faction.DevelopmentStage;
        faction.Aggression = Math.Min(100, faction.Aggression + Math.Max(0, faction.DevelopmentStage - stageBefore) * 5);
        faction.LastProcessedAt = payload.ProcessedAt;
        faction.NextDecisionAt = payload.ProcessedAt.AddHours(1);
        faction.UpdatedAt = payload.ProcessedAt;
        leader.LastProcessedAt = payload.ProcessedAt;
        leader.UpdatedAt = payload.ProcessedAt;

        foreach (var creature in creatures)
        {
            if (creature.FactionId is null &&
                creature.Status == CreatureStatus.Dead &&
                creature.RespawnAt <= payload.ProcessedAt)
            {
                creature.Status = CreatureStatus.Alive;
                creature.Health = creature.MaximumHealth;
                creature.RespawnAt = null;
                creature.PositionX = creature.SpawnX;
                creature.PositionY = creature.SpawnY;
                creature.PositionZ = creature.SpawnZ;
            }
            else if (creature.Status == CreatureStatus.Alive && creature.Health < creature.MaximumHealth)
            {
                creature.Health = Math.Min(creature.MaximumHealth, creature.Health + payload.WorldHours * 2);
            }

            creature.LastProcessedAt = payload.ProcessedAt;
            creature.UpdatedAt = payload.ProcessedAt;
        }

        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "faction_progressed",
            Title = "The Darkwood Clan worked while Stonehaven slept",
            Description = $"During {payload.WorldHours} world hours the clan grew from {populationBefore} to {faction.Population} goblins; food {foodBefore}→{resources[ResourceKind.Food].Amount}, wood {woodBefore}→{resources[ResourceKind.Wood].Amount}, stone {stoneBefore}→{resources[ResourceKind.Stone].Amount}, iron {ironBefore}→{resources[ResourceKind.Iron].Amount}.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = faction.Id,
            CreatureId = leader.Id,
            OccurredAt = payload.ProcessedAt,
            ImportanceLevel = 1,
            CreatedAt = payload.ProcessedAt,
            UpdatedAt = payload.ProcessedAt
        });

        if (faction.Population > populationBefore)
        {
            AddHistory(
                "population_growth",
                "New fires appeared beyond the northern road",
                $"The Darkwood Clan population increased from {populationBefore} to {faction.Population}.",
                2,
                faction,
                leader,
                payload.ProcessedAt);
        }


        if (settlement.Population > stonehavenPopulationBefore)
        {
            AddHistory(
                "stonehaven_population_growth",
                "New households joined Stonehaven",
                $"Stonehaven grew from {stonehavenPopulationBefore} to {settlement.Population} living residents. Every new resident has a persistent name, role, schedule, and status in the village roster.",
                2,
                faction,
                leader,
                payload.ProcessedAt);
        }

        if (faction.DevelopmentStage > stageBefore)
        {
            AddHistory(
                "camp_upgrade",
                faction.DevelopmentStage == 2
                    ? "The Darkwood encampment became an established camp"
                    : "Darkwood raised walls and a watchtower",
                $"The Darkwood Clan advanced to Stage {faction.DevelopmentStage}: {StageName(faction.DevelopmentStage)}.",
                4,
                faction,
                leader,
                payload.ProcessedAt);
        }

        if (!string.Equals(titleBefore, leader.Title, StringComparison.Ordinal))
        {
            AddHistory(
                "leader_promoted",
                $"{leader.Name} became {leader.Title}",
                $"Survival, resources, and the clan's growing strength elevated {leader.Name} from {titleBefore ?? "an untested chief"} to {leader.Title}.",
                4,
                faction,
                leader,
                payload.ProcessedAt);
        }
        else if (leaderLeveled)
        {
            AddHistory(
                "leader_leveled",
                $"{leader.Name} reached level {leader.Level}",
                $"The Darkwood leader trained and grew stronger while the valley continued around him.",
                2,
                faction,
                leader,
                payload.ProcessedAt);
        }

        settlement.UpdatedAt = payload.ProcessedAt;
        await raidSimulation.EvaluateWorldProgressionAsync(
            payload.WorldHours,
            payload.ProcessedAt,
            cancellationToken);
    }

    private void AdvanceSettlementConstruction(
        int worldHours,
        DateTimeOffset processedAt,
        Faction faction,
        Settlement settlement,
        IReadOnlyCollection<SettlementResident> residents,
        IReadOnlyCollection<Creature> creatures,
        Dictionary<ResourceKind, FactionResource> factionResources,
        ConstructionProject stonehavenWall,
        ConstructionProject darkwoodPalisade)
    {
        var nessaWorking = residents.Any(x =>
            x.Name.Equals("Nessa", StringComparison.OrdinalIgnoreCase) &&
            x.Status is ResidentStatus.Active or ResidentStatus.Injured &&
            x.Health > 0);
        var dainWorking = residents.Any(x =>
            x.Name.Equals("Dain", StringComparison.OrdinalIgnoreCase) &&
            x.Status is ResidentStatus.Active or ResidentStatus.Injured &&
            x.Health > 0);
        var skritWorking = creatures.Any(x =>
            x.Name.Equals("Skrit", StringComparison.OrdinalIgnoreCase) &&
            x.Status == CreatureStatus.Alive &&
            IsAtDarkwoodCamp(x));
        var vrakWorking = creatures.Any(x =>
            x.Name.Equals("Vrak", StringComparison.OrdinalIgnoreCase) &&
            x.Status == CreatureStatus.Alive &&
            IsAtDarkwoodCamp(x));

        var nessaWood = 0;
        var dainStone = 0;
        var skritWood = 0;
        var vrakStone = 0;
        for (var hour = 0; hour < worldHours; hour++)
        {
            var stonehavenReserve = Math.Max(16, settlement.Population * 2);
            var wallWork = ApplySimulatedProjectWork(
                stonehavenWall,
                nessaWorking ? Math.Min(5, Math.Max(0, settlement.Wood - stonehavenReserve)) : 0,
                dainWorking ? Math.Min(4, Math.Max(0, settlement.Stone - stonehavenReserve)) : 0,
                "Nessa and Dain",
                processedAt,
                faction,
                settlement);
            settlement.Wood -= wallWork.Wood;
            settlement.Stone -= wallWork.Stone;
            nessaWood += wallWork.Wood;
            dainStone += wallWork.Stone;

            var darkwoodWood = factionResources[ResourceKind.Wood];
            var darkwoodStone = factionResources[ResourceKind.Stone];
            var darkwoodReserve = Math.Max(14, faction.Population * 2);
            var palisadeWork = ApplySimulatedProjectWork(
                darkwoodPalisade,
                skritWorking ? (int)Math.Min(6, Math.Max(0, darkwoodWood.Amount - darkwoodReserve)) : 0,
                vrakWorking ? (int)Math.Min(4, Math.Max(0, darkwoodStone.Amount - darkwoodReserve)) : 0,
                "Skrit and Vrak",
                processedAt,
                faction,
                settlement);
            darkwoodWood.Amount -= palisadeWork.Wood;
            darkwoodStone.Amount -= palisadeWork.Stone;
            skritWood += palisadeWork.Wood;
            vrakStone += palisadeWork.Stone;
        }

        AddSimulatedContribution(stonehavenWall.Id, "Nessa", ResourceKind.Wood, nessaWood, processedAt);
        AddSimulatedContribution(stonehavenWall.Id, "Dain", ResourceKind.Stone, dainStone, processedAt);
        AddSimulatedContribution(darkwoodPalisade.Id, "Skrit", ResourceKind.Wood, skritWood, processedAt);
        AddSimulatedContribution(darkwoodPalisade.Id, "Vrak", ResourceKind.Stone, vrakStone, processedAt);
    }

    private (int Wood, int Stone) ApplySimulatedProjectWork(
        ConstructionProject project,
        int wood,
        int stone,
        string workers,
        DateTimeOffset processedAt,
        Faction faction,
        Settlement settlement)
    {
        if (project.CompletedAt is not null || project.CurrentLevel >= project.MaximumLevel)
        {
            return (0, 0);
        }

        var woodApplied = Math.Min(wood, Math.Max(0, project.WoodRequired - project.WoodContributed));
        var stoneApplied = Math.Min(stone, Math.Max(0, project.StoneRequired - project.StoneContributed));
        project.WoodContributed += woodApplied;
        project.StoneContributed += stoneApplied;
        if (woodApplied > 0 || stoneApplied > 0)
        {
            project.LastNpcContributionAt = processedAt;
            project.UpdatedAt = processedAt;
        }

        if (project.WoodContributed < project.WoodRequired ||
            project.StoneContributed < project.StoneRequired)
        {
            return (woodApplied, stoneApplied);
        }

        project.CurrentLevel++;
        if (project.Id == LivingRealmsDbContext.StonehavenWallProjectId)
        {
            settlement.DefenseRating += 12;
            settlement.GuardStrength += 4;
            settlement.StructuralIntegrity += 220;
            settlement.UpdatedAt = processedAt;
        }
        else if (project.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId)
        {
            faction.MilitaryStrength += 14;
            faction.Morale = Math.Min(100, faction.Morale + 3);
            faction.UpdatedAt = processedAt;
        }

        var completed = project.CurrentLevel >= project.MaximumLevel;
        if (completed)
        {
            project.CompletedAt = processedAt;
        }
        else
        {
            project.WoodContributed = 0;
            project.StoneContributed = 0;
            project.WoodRequired = (int)MathF.Ceiling(project.WoodRequired * 1.35f);
            project.StoneRequired = (int)MathF.Ceiling(project.StoneRequired * 1.35f);
        }
        project.UpdatedAt = processedAt;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = completed ? "construction_completed" : "construction_upgraded",
            Title = completed
                ? $"{project.Name} reached its final tier"
                : $"{project.Name} reached level {project.CurrentLevel}",
            Description =
                $"{workers} delivered the final materials from their settlement or faction stores. " +
                "The consumed timber and stone were removed from those stores before the project advanced.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = project.FactionId,
            OccurredAt = processedAt,
            ImportanceLevel = 3,
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        });
        return (woodApplied, stoneApplied);
    }

    private void AddSimulatedContribution(
        Guid projectId,
        string contributor,
        ResourceKind kind,
        int amount,
        DateTimeOffset processedAt)
    {
        if (amount <= 0)
        {
            return;
        }

        database.ResourceContributions.Add(new ResourceContribution
        {
            ConstructionProjectId = projectId,
            ContributorName = contributor,
            Kind = kind,
            Amount = amount,
            Source = "WorldSimulation",
            OccurredAt = processedAt,
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        });
    }

    private static bool IsAtDarkwoodCamp(Creature creature) =>
        creature.PositionX < -80.0f && creature.PositionZ < -70.0f;

    private async Task<int> RecoverInterruptedEventsAsync(
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var staleBefore = processedAt.AddMinutes(-Math.Max(1, options.Value.ProcessingTimeoutMinutes));
        if (database.Database.IsRelational())
        {
            return await database.ScheduledEvents
                .Where(x => x.Status == ScheduledEventStatus.Processing && x.StartedAt < staleBefore)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.Status, ScheduledEventStatus.Pending)
                    .SetProperty(x => x.StartedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.RetryCount, x => x.RetryCount + 1)
                    .SetProperty(x => x.ErrorMessage, "Recovered after an interrupted worker run.")
                    .SetProperty(x => x.UpdatedAt, processedAt), cancellationToken);
        }

        var staleEvents = await database.ScheduledEvents
            .Where(x => x.Status == ScheduledEventStatus.Processing && x.StartedAt < staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var staleEvent in staleEvents)
        {
            staleEvent.Status = ScheduledEventStatus.Pending;
            staleEvent.StartedAt = null;
            staleEvent.RetryCount++;
            staleEvent.ErrorMessage = "Recovered after an interrupted worker run.";
            staleEvent.UpdatedAt = processedAt;
        }
        await database.SaveChangesAsync(cancellationToken);
        return staleEvents.Count;
    }

    private static void AddResource(FactionResource resource, long amount) =>
        resource.Amount = Math.Clamp(resource.Amount + amount, 0, resource.Capacity);

    private static void ResetResource(
        IEnumerable<FactionResource> resources,
        ResourceKind kind,
        long amount,
        long capacity,
        DateTimeOffset resetAt)
    {
        var resource = resources.Single(x => x.Kind == kind);
        resource.Amount = amount;
        resource.Capacity = capacity;
        resource.UpdatedAt = resetAt;
    }

    private static void ExpandCapacities(
        Dictionary<ResourceKind, FactionResource> resources,
        long food,
        long wood,
        long stone,
        long iron,
        long gold)
    {
        resources[ResourceKind.Food].Capacity = Math.Max(resources[ResourceKind.Food].Capacity, food);
        resources[ResourceKind.Wood].Capacity = Math.Max(resources[ResourceKind.Wood].Capacity, wood);
        resources[ResourceKind.Stone].Capacity = Math.Max(resources[ResourceKind.Stone].Capacity, stone);
        resources[ResourceKind.Iron].Capacity = Math.Max(resources[ResourceKind.Iron].Capacity, iron);
        resources[ResourceKind.Gold].Capacity = Math.Max(resources[ResourceKind.Gold].Capacity, gold);
    }

    private void AddStructure(Faction faction, string structureType, DateTimeOffset completedAt)
    {
        if (faction.Structures.Any(x => x.StructureType == structureType))
        {
            return;
        }

        database.FactionStructures.Add(new FactionStructure
        {
            FactionId = faction.Id,
            Faction = faction,
            StructureType = structureType,
            Level = 1,
            Health = 100,
            CompletedAt = completedAt,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        });
    }

    private void AddHistory(
        string eventType,
        string title,
        string description,
        int importance,
        Faction faction,
        Creature leader,
        DateTimeOffset occurredAt)
    {
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = eventType,
            Title = title,
            Description = description,
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = faction.Id,
            CreatureId = leader.Id,
            OccurredAt = occurredAt,
            ImportanceLevel = importance,
            CreatedAt = occurredAt,
            UpdatedAt = occurredAt
        });
    }

    public static string StageName(int stage) => stage switch
    {
        1 => "Encampment",
        2 => "Established Camp",
        3 => "Fortified Camp",
        _ => "Unknown"
    };

    private static DateTimeOffset CentralNow()
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }

        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Processed {WorldHours} world hours from {Source} at {CentralTime}")]
    private static partial void LogProgressionProcessed(
        ILogger logger,
        int worldHours,
        string source,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "World event {EventId} failed at {CentralTime}")]
    private static partial void LogProgressionFailed(
        ILogger logger,
        Exception exception,
        Guid eventId,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Warning,
        Message = "Development world simulation reset at {CentralTime}")]
    private static partial void LogWorldReset(ILogger logger, DateTimeOffset centralTime);

    private sealed record ProgressionPayload(int WorldHours, DateTimeOffset ProcessedAt, string Source);
}

public sealed class WorldSimulationOptions
{
    public const string SectionName = "WorldSimulation";
    public double WorldMinutesPerRealMinute { get; set; } = 1;
    public int MaximumCatchUpWorldHours { get; set; } = 168;
    public int MaximumEventsPerRun { get; set; } = 12;
    public int MaximumEventRetries { get; set; } = 3;
    public int ProcessingTimeoutMinutes { get; set; } = 5;
}

public sealed record WorldSimulationRunResult(int EventsProcessed, int EventsRecovered, int WorldHoursRequested);
