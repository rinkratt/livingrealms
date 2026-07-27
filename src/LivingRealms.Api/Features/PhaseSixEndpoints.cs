using System.Security.Claims;
using LivingRealms.Api.Logging;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LivingRealms.Api.Features;

public static class PhaseSixEndpoints
{
    public static IEndpointRouteBuilder MapPhaseSixEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var world = endpoints.MapGroup("/api/v1/world").RequireAuthorization();
        world.MapGet("/state", GetWorldStateAsync);
        world.MapGet("/history", GetWorldHistoryAsync);
        world.MapPost("/advance", AdvanceWorldAsync).RequireRateLimiting("world-control");
        world.MapPost("/reset", ResetWorldAsync).RequireRateLimiting("world-control");
        return endpoints;
    }

    private static async Task<IResult> GetWorldStateAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        WorldPopulationService population,
        IHostEnvironment environment,
        IOptions<WorldSimulationOptions> options,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await BuildWorldStateAsync(
            database,
            population,
            CanControlWorld(context.User, environment),
            options.Value,
            cancellationToken));
    }

    private static async Task<IResult> GetWorldHistoryAsync(
        int? limit,
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit ?? 20, 1, 50);
        var historyRows = await database.WorldHistory.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
        var history = historyRows.Select(x => new HistoryResponse(
            x.Id,
            x.EventType,
            x.Title,
            x.Description,
            x.ImportanceLevel,
            CentralClock.Convert(x.OccurredAt))).ToArray();
        return Results.Ok(history);
    }

    private static async Task<IResult> AdvanceWorldAsync(
        AdvanceWorldRequest request,
        HttpContext context,
        LivingRealmsDbContext database,
        WorldSimulationService simulation,
        WorldPopulationService population,
        IHostEnvironment environment,
        IOptions<WorldSimulationOptions> options,
        ILoggerFactory loggerFactory)
    {
        if (!context.User.IsInRole("Administrator"))
        {
            return Results.Forbid();
        }

        if (!CanAccelerate(environment))
        {
            return Results.NotFound();
        }

        if (request.Hours is < 1 or > 168)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["hours"] = ["Choose between 1 and 168 world hours."]
            });
        }

        var now = DateTimeOffset.UtcNow;
        var result = await simulation.AdvanceForTestingAsync(request.Hours, now, context.RequestAborted);
        var accountIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(accountIdText, out var accountId))
        {
            var centralTime = CentralClock.Now;
            var auditLogger = loggerFactory.CreateLogger("LivingRealms.Audit");
            AuditLog.WorldAdvanced(
                auditLogger,
                accountId,
                request.Hours,
                centralTime);
        }

        var state = await BuildWorldStateAsync(database, population, true, options.Value, context.RequestAborted);
        return Results.Ok(new AdvanceWorldResponse(result, state));
    }

    private static async Task<IResult> ResetWorldAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        WorldSimulationService simulation,
        WorldPopulationService population,
        IHostEnvironment environment,
        IOptions<WorldSimulationOptions> options,
        ILoggerFactory loggerFactory)
    {
        if (!context.User.IsInRole("Administrator"))
        {
            return Results.Forbid();
        }

        if (!CanAccelerate(environment))
        {
            return Results.NotFound();
        }

        await simulation.ResetForTestingAsync(DateTimeOffset.UtcNow, context.RequestAborted);
        var accountIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(accountIdText, out var accountId))
        {
            AuditLog.WorldReset(
                loggerFactory.CreateLogger("LivingRealms.Audit"),
                accountId,
                CentralClock.Now);
        }

        return Results.Ok(await BuildWorldStateAsync(database, population, true, options.Value, context.RequestAborted));
    }

    private static async Task<WorldStateResponse> BuildWorldStateAsync(
        LivingRealmsDbContext database,
        WorldPopulationService population,
        bool canAccelerate,
        WorldSimulationOptions options,
        CancellationToken cancellationToken)
    {
        await population.EnsureDarkwoodClanMembersAsync(cancellationToken: cancellationToken);
        await population.EnsureStonehavenResidentsAsync(cancellationToken: cancellationToken);
        await population.EnsureHuntableWildlifeAsync(cancellationToken: cancellationToken);
        var faction = await database.Factions.AsNoTracking()
            .Include(x => x.Resources)
            .Include(x => x.Structures)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var leader = await database.Creatures.AsNoTracking()
            .Where(x => x.FactionId == faction.Id)
            .OrderByDescending(x => x.Id == faction.LeaderCreatureId)
            .ThenByDescending(x => x.Status == CreatureStatus.Alive && x.Health > 0)
            .ThenByDescending(x => x.Leadership)
            .ThenByDescending(x => x.Level)
            .FirstAsync(cancellationToken);
        var settlement = await database.Settlements.AsNoTracking()
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        var stonehavenLeader = await database.SettlementResidents.AsNoTracking()
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenLeaderResidentId, cancellationToken);
        var livingStonehavenResidents = await database.SettlementResidents.AsNoTracking()
            .CountAsync(x => x.SettlementId == settlement.Id &&
                             x.Health > 0 &&
                             (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured),
                cancellationToken);
        var stonehavenResidents = await database.SettlementResidents.AsNoTracking()
            .Where(x => x.SettlementId == settlement.Id)
            .ToArrayAsync(cancellationToken);
        var darkwoodMembers = await database.Creatures.AsNoTracking()
            .Where(x => x.FactionId == faction.Id)
            .ToArrayAsync(cancellationToken);
        var huntableWildlife = await database.Creatures.AsNoTracking()
            .Where(x => x.FactionId == null &&
                        (x.SpeciesId == LivingRealmsDbContext.ForestRatSpeciesId ||
                         x.SpeciesId == LivingRealmsDbContext.PrairieWolfSpeciesId))
            .ToArrayAsync(cancellationToken);
        var availableWildlife = huntableWildlife.Count(x =>
            x.Status == CreatureStatus.Alive && x.Health > 0);
        var stonehavenFood = WorldSurvivalService.CalculateStonehaven(
            stonehavenResidents,
            settlement.Food,
            availableWildlife);
        var darkwoodFood = WorldSurvivalService.CalculateDarkwood(
            darkwoodMembers,
            faction.Resources.Single(x => x.Kind == ResourceKind.Food).Amount,
            availableWildlife);
        var combatReadyStonehavenResidents = await database.SettlementResidents.AsNoTracking()
            .CountAsync(x => x.SettlementId == settlement.Id &&
                             x.CanFight &&
                             x.Health > 0 &&
                             (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured),
                cancellationToken);
        var raidReadyDarkwoodFighters = await database.Creatures.AsNoTracking()
            .CountAsync(x => x.FactionId == faction.Id &&
                             x.Id != faction.LeaderCreatureId &&
                             x.Role != "Raid Attacker" &&
                             x.Status == CreatureStatus.Alive &&
                             x.Health > 0,
                cancellationToken);
        var darkwoodRaidActive = await database.SettlementRaids.AsNoTracking()
            .AnyAsync(x => x.Status == SettlementRaidStatus.Active, cancellationToken);
        var counterattack = await database.StonehavenAssaults.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var counterattackActive = counterattack is not null &&
                                  counterattack.Status is StonehavenAssaultStatus.Assembling or
                                      StonehavenAssaultStatus.Marching or
                                      StonehavenAssaultStatus.FightingGoblins or
                                      StonehavenAssaultStatus.AttackingCamp;
        var activeAdministratorCutoff = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(2));
        var administratorOnline = await database.PlayerSessions.AsNoTracking()
            .AnyAsync(x => x.CharacterId != null &&
                           x.DisconnectedAt == null &&
                           x.ExpiresAt > DateTimeOffset.UtcNow &&
                           x.LastSeenAt != null &&
                           x.LastSeenAt >= activeAdministratorCutoff &&
                           x.Account.IsAdministrator,
                cancellationToken);
        var anyBattleActive = darkwoodRaidActive || counterattackActive;
        var darkwoodRaidReady =
            raidReadyDarkwoodFighters >= WorldPopulationService.AutomaticDarkwoodRaidersRequired &&
            !anyBattleActive;
        var counterattackReady =
            faction.DevelopmentStage >= 3 &&
            livingStonehavenResidents >= WorldPopulationService.StonehavenAssaultSoldiersRequired &&
            !anyBattleActive;
        var eventCounts = await database.ScheduledEvents.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var lastCompletedEventAt = await database.ScheduledEvents.AsNoTracking()
            .Where(x => x.Status == ScheduledEventStatus.Completed && x.CompletedAt != null)
            .MaxAsync(x => (DateTimeOffset?)x.CompletedAt, cancellationToken);
        var recentHistoryRows = await database.WorldHistory.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(8)
            .ToArrayAsync(cancellationToken);
        var recentHistory = recentHistoryRows.Select(x => new HistoryResponse(
            x.Id,
            x.EventType,
            x.Title,
            x.Description,
            x.ImportanceLevel,
            CentralClock.Convert(x.OccurredAt))).ToArray();
        var destructibleStructures = await new WorldStructureService(database)
            .GetStatesAsync(cancellationToken: cancellationToken);

        var resources = faction.Resources
            .OrderBy(x => x.Kind)
            .Select(x => new ResourceResponse(x.Kind.ToString(), x.Amount, x.Capacity))
            .ToArray();
        var structures = faction.Structures
            .OrderBy(x => x.CompletedAt)
            .Select(x => new StructureResponse(x.StructureType, x.Level, x.Health))
            .ToArray();
        var pending = eventCounts.GetValueOrDefault(ScheduledEventStatus.Pending) +
                      eventCounts.GetValueOrDefault(ScheduledEventStatus.Processing);

        return new WorldStateResponse(
            faction.SimulatedHours,
            (int)(faction.SimulatedHours / 24) + 1,
            options.WorldMinutesPerRealMinute == 1
                ? "Real time: 1 real minute = 1 world minute"
                : $"1 real minute = {options.WorldMinutesPerRealMinute:0.##} world minute(s)",
            canAccelerate,
            new FactionResponse(
                faction.Id,
                faction.Name,
                faction.Population,
                faction.PopulationCapacity,
                faction.DevelopmentStage,
                WorldSimulationService.StageName(faction.DevelopmentStage),
                faction.TerritorySize,
                faction.Aggression,
                faction.Morale,
                faction.TechnologyLevel,
                faction.MilitaryStrength,
                resources,
                structures,
                new LeaderResponse(
                    leader.Id,
                    leader.Name,
                    leader.Title ?? "Goblin Chief",
                    leader.Level,
                    leader.Experience,
                    leader.Leadership,
                    leader.Health,
                    leader.MaximumHealth,
                    leader.Attack,
                    leader.Defense,
                    leader.Status.ToString()),
                CentralClock.Convert(faction.LastProcessedAt),
                CentralClock.Convert(faction.NextDecisionAt)),
            new SettlementResponse(
                settlement.Id,
                settlement.Name,
                settlement.Population,
                livingStonehavenResidents,
                combatReadyStonehavenResidents,
                WorldPopulationService.StonehavenHousingCapacity,
                settlement.Food,
                settlement.Wood,
                settlement.Stone,
                settlement.Iron,
                settlement.StructuralIntegrity,
                settlement.DefenseRating,
                settlement.GuardStrength,
                settlement.IsDestroyed,
                new SettlementLeaderResponse(
                    stonehavenLeader.Id,
                    stonehavenLeader.Name,
                    "Reeve of Stonehaven",
                    stonehavenLeader.Role,
                    stonehavenLeader.Health,
                    stonehavenLeader.MaximumHealth,
                    stonehavenLeader.Status.ToString(),
                    stonehavenLeader.PrimarySkill,
                    stonehavenLeader.SkillLevel,
                    stonehavenLeader.Trait,
                    stonehavenLeader.IsMajor,
                    stonehavenLeader.MemorySummary)),
            new SurvivalResponse(
                ToFoodEconomyResponse(stonehavenFood),
                ToFoodEconomyResponse(darkwoodFood),
                new WildlifeResponse(
                    huntableWildlife.Length,
                    availableWildlife,
                    huntableWildlife.Length - availableWildlife)),
            destructibleStructures,
            new WorldEventReadinessResponse(
                new TriggerReadinessResponse(
                    "Darkwood raid on Stonehaven",
                    raidReadyDarkwoodFighters,
                    WorldPopulationService.AutomaticDarkwoodRaidersRequired,
                    darkwoodRaidReady,
                    darkwoodRaidActive,
                    administratorOnline,
                    darkwoodRaidActive
                        ? "ACTIVE: Darkwood is attacking Stonehaven now."
                        : darkwoodRaidReady
                            ? administratorOnline
                                ? "READY: an online administrator may authorize Darkwood's march."
                                : "READY: waiting for an administrator to log into the game."
                            : $"Darkwood needs {WorldPopulationService.AutomaticDarkwoodRaidersRequired} living fighters, not counting its current leader. {raidReadyDarkwoodFighters} are ready now."),
                new TriggerReadinessResponse(
                    "Stonehaven counterattack on Darkwood",
                    livingStonehavenResidents,
                    WorldPopulationService.StonehavenAssaultSoldiersRequired,
                    counterattackReady,
                    counterattackActive,
                    administratorOnline,
                    counterattackActive
                        ? $"ACTIVE: {FormatAssaultPhase(counterattack!.Status)}"
                        : counterattackReady
                            ? administratorOnline
                                ? "READY: an online administrator may authorize Stonehaven's counterattack."
                                : "READY: waiting for an administrator to log into the game."
                            : $"Stonehaven needs {WorldPopulationService.StonehavenAssaultSoldiersRequired} living residents and a completed level 3 Darkwood camp. Stonehaven has {livingStonehavenResidents}; Darkwood is level {faction.DevelopmentStage}/3.")),
            new EventQueueResponse(
                pending,
                eventCounts.GetValueOrDefault(ScheduledEventStatus.Completed),
                eventCounts.GetValueOrDefault(ScheduledEventStatus.Failed),
                lastCompletedEventAt is null ? null : CentralClock.Convert(lastCompletedEventAt.Value)),
            recentHistory,
            CentralClock.Now);
    }

    private static FoodEconomyResponse ToFoodEconomyResponse(FoodEconomySnapshot snapshot) =>
        new(
            snapshot.Population,
            snapshot.StoredFood,
            snapshot.Farmers,
            snapshot.Fishers,
            snapshot.Hunters,
            snapshot.FarmerProductionPerHour,
            snapshot.FisherProductionPerHour,
            snapshot.HunterProductionPerHour,
            snapshot.FoodProducedPerHour,
            snapshot.FoodConsumedPerHour,
            snapshot.NetFoodPerHour,
            snapshot.IsShortage,
            snapshot.HoursOfFoodRemaining,
            snapshot.RecommendedRecruitmentRole);

    private static string FormatAssaultPhase(StonehavenAssaultStatus status) => status switch
    {
        StonehavenAssaultStatus.Assembling => "Guard Captain Mira is assembling the counterattack at Stonehaven's gate.",
        StonehavenAssaultStatus.Marching => "Twenty Stonehaven soldiers and militia are marching to Darkwood.",
        StonehavenAssaultStatus.FightingGoblins => "Stonehaven's force is fighting the goblin defenders at their camp.",
        StonehavenAssaultStatus.AttackingCamp => "The goblins are down; Stonehaven's survivors are destroying the camp.",
        _ => status.ToString()
    };

    private static bool CanAccelerate(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing");

    private static bool CanControlWorld(ClaimsPrincipal user, IHostEnvironment environment) =>
        user.IsInRole("Administrator") && CanAccelerate(environment);

    public sealed record AdvanceWorldRequest(int Hours);
    public sealed record AdvanceWorldResponse(WorldSimulationRunResult Run, WorldStateResponse World);
    public sealed record WorldStateResponse(
        long SimulatedHours,
        int WorldDay,
        string SimulationSpeed,
        bool CanAccelerate,
        FactionResponse Faction,
        SettlementResponse Settlement,
        SurvivalResponse Survival,
        IReadOnlyCollection<WorldStructureState> Structures,
        WorldEventReadinessResponse EventReadiness,
        EventQueueResponse Events,
        IReadOnlyCollection<HistoryResponse> RecentHistory,
        DateTimeOffset ServerTimeCentral);
    public sealed record FactionResponse(
        Guid Id,
        string Name,
        int Population,
        int PopulationCapacity,
        int DevelopmentStage,
        string StageName,
        int TerritorySize,
        int Aggression,
        int Morale,
        int TechnologyLevel,
        int MilitaryStrength,
        IReadOnlyCollection<ResourceResponse> Resources,
        IReadOnlyCollection<StructureResponse> Structures,
        LeaderResponse Leader,
        DateTimeOffset LastProcessedCentral,
        DateTimeOffset NextDecisionCentral);
    public sealed record ResourceResponse(string Kind, long Amount, long Capacity);
    public sealed record StructureResponse(string Name, int Level, int Health);
    public sealed record LeaderResponse(
        Guid Id,
        string Name,
        string Title,
        int Level,
        long Experience,
        int Leadership,
        int Health,
        int MaximumHealth,
        int Attack,
        int Defense,
        string Status);
    public sealed record SettlementResponse(
        Guid Id,
        string Name,
        int Population,
        int LivingResidents,
        int CombatReadyResidents,
        int HousingCapacity,
        int Food,
        int Wood,
        int Stone,
        int Iron,
        int StructuralIntegrity,
        int DefenseRating,
        int GuardStrength,
        bool IsDestroyed,
        SettlementLeaderResponse Leader);
    public sealed record SurvivalResponse(
        FoodEconomyResponse Stonehaven,
        FoodEconomyResponse Darkwood,
        WildlifeResponse Wildlife);
    public sealed record FoodEconomyResponse(
        int Population,
        int FoodStored,
        int Farmers,
        int Fishers,
        int Hunters,
        int FarmerProductionPerHour,
        int FisherProductionPerHour,
        int HunterProductionPerHour,
        int FoodProducedPerHour,
        int FoodConsumedPerHour,
        int NetFoodPerHour,
        bool IsShortage,
        int HoursOfFoodRemaining,
        string RecommendedRecruitmentRole);
    public sealed record WildlifeResponse(int Total, int Available, int Respawning);
    public sealed record SettlementLeaderResponse(
        Guid Id,
        string Name,
        string Title,
        string Role,
        int Health,
        int MaximumHealth,
        string Status,
        string PrimarySkill,
        int SkillLevel,
        string Trait,
        bool IsMajor,
        string MemorySummary);
    public sealed record WorldEventReadinessResponse(
        TriggerReadinessResponse DarkwoodRaid,
        TriggerReadinessResponse StonehavenCounterattack);
    public sealed record TriggerReadinessResponse(
        string Name,
        int Current,
        int Required,
        bool Ready,
        bool Active,
        bool AdministratorOnline,
        string Explanation);
    public sealed record EventQueueResponse(
        int Pending,
        int Completed,
        int Failed,
        DateTimeOffset? LastCompletedCentral);
    public sealed record HistoryResponse(
        Guid Id,
        string EventType,
        string Title,
        string Description,
        int ImportanceLevel,
        DateTimeOffset OccurredAtCentral);
}
