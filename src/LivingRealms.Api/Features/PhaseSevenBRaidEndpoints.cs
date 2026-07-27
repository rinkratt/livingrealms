using System.Security.Claims;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class PhaseSevenBRaidEndpoints
{
    private static readonly TimeSpan ActiveAdministratorWindow = TimeSpan.FromMinutes(2);

    public static IEndpointRouteBuilder MapPhaseSevenBRaidEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var raid = endpoints.MapGroup("/api/v1/world/raid").RequireAuthorization();
        raid.MapGet("", GetRaidAsync);
        raid.MapPost("/start", StartRaidAsync).RequireRateLimiting("gameplay");
        raid.MapPost("/counterattack/start", StartCounterattackAsync).RequireRateLimiting("gameplay");
        raid.MapPost("/advance", AdvanceRaidAsync).RequireRateLimiting("gameplay");
        return endpoints;
    }

    private static async Task<IResult> GetRaidAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        IHostEnvironment environment)
    {
        if (!await HasSelectedCharacterAsync(context, database))
        {
            return Results.Conflict(new ErrorResponse("Select a character before loading the Stonehaven raid."));
        }
        return Results.Ok(await BuildStateAsync(
            database,
            environment,
            context.User.IsInRole("Administrator"),
            context.RequestAborted));
    }

    private static async Task<IResult> StartRaidAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        RaidSimulationService simulation,
        IHostEnvironment environment)
    {
        if (!context.User.IsInRole("Administrator"))
        {
            return Results.Forbid();
        }
        if (!await HasSelectedCharacterAsync(context, database))
        {
            return Results.Conflict(new ErrorResponse("Select a character before starting the raid playtest."));
        }

        var state = await BuildStateAsync(database, environment, true, context.RequestAborted);
        var developmentPlaytest = environment.IsEnvironment("Testing");
        var fullCampaignAuthorization = state.CanStartDarkwoodRaid;
        if (!fullCampaignAuthorization && !(developmentPlaytest && state.CanStartPlaytest))
        {
            return Results.Conflict(new ErrorResponse(
                state.AdministratorOnline
                    ? $"Darkwood is not ready. It needs {WorldPopulationService.AutomaticDarkwoodRaidersRequired} available fighters and no active conflict."
                    : "A game administrator must be online before a battle can be authorized."));
        }

        await simulation.StartRaidAsync(
            DateTimeOffset.UtcNow,
            fullCampaignAuthorization ? "administrator-authorization" : "development-control",
            context.RequestAborted);
        return Results.Ok(await BuildStateAsync(database, environment, true, context.RequestAborted));
    }

    private static async Task<IResult> StartCounterattackAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        RaidSimulationService simulation,
        IHostEnvironment environment)
    {
        if (!context.User.IsInRole("Administrator"))
        {
            return Results.Forbid();
        }
        if (!await HasSelectedCharacterAsync(context, database))
        {
            return Results.Conflict(new ErrorResponse(
                "Select a character before authorizing Stonehaven's counterattack."));
        }

        var state = await BuildStateAsync(database, environment, true, context.RequestAborted);
        if (!state.CanStartCounterattack)
        {
            return Results.Conflict(new ErrorResponse(
                state.AdministratorOnline
                    ? $"Stonehaven is not ready. It needs {WorldPopulationService.StonehavenAssaultSoldiersRequired} living residents, a completed level 3 Darkwood camp, and no active conflict."
                    : "A game administrator must be online before a battle can be authorized."));
        }

        await simulation.StartStonehavenAssaultAsync(DateTimeOffset.UtcNow, context.RequestAborted);
        return Results.Ok(await BuildStateAsync(database, environment, true, context.RequestAborted));
    }

    private static async Task<IResult> AdvanceRaidAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        RaidSimulationService simulation,
        IHostEnvironment environment)
    {
        if (!await HasSelectedCharacterAsync(context, database))
        {
            return Results.Conflict(new ErrorResponse("Select a character before advancing the raid."));
        }

        await simulation.AdvanceActiveConflictAsync(DateTimeOffset.UtcNow, cancellationToken: context.RequestAborted);
        return Results.Ok(await BuildStateAsync(
            database,
            environment,
            context.User.IsInRole("Administrator"),
            context.RequestAborted));
    }

    private static async Task<RaidStateResponse> BuildStateAsync(
        LivingRealmsDbContext database,
        IHostEnvironment environment,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        var raid = await database.SettlementRaids.AsNoTracking()
            .Include(x => x.Attackers)
            .ThenInclude(x => x.Creature)
            .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var hasSurvivingRaiders = raid?.Attackers.Any(x =>
            !x.IsDefeated &&
            x.Creature.Status == CreatureStatus.Alive &&
            x.Creature.Health > 0) == true;
        var counterattack = await database.StonehavenAssaults.AsNoTracking()
            .Include(x => x.Members)
            .ThenInclude(x => x.Resident)
            .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var counterattackActive = counterattack?.Status is
            StonehavenAssaultStatus.Assembling or
            StonehavenAssaultStatus.Marching or
            StonehavenAssaultStatus.FightingGoblins or
            StonehavenAssaultStatus.AttackingCamp;
        var raidActive = raid?.Status == SettlementRaidStatus.Active;
        var faction = await database.Factions.AsNoTracking()
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var availableDarkwoodFighters = await database.Creatures.AsNoTracking()
            .CountAsync(x => x.FactionId == faction.Id &&
                             x.Id != faction.LeaderCreatureId &&
                             x.Status == CreatureStatus.Alive &&
                             x.Health > 0 &&
                             x.Role != "Raid Attacker",
                cancellationToken);
        var livingStonehavenResidents = await database.SettlementResidents.AsNoTracking()
            .CountAsync(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                             x.Health > 0 &&
                             (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured),
                cancellationToken);
        var administratorOnline = await IsAdministratorOnlineAsync(database, cancellationToken);
        var settlementsHealthy = await database.SettlementRecoveries.AsNoTracking()
            .AllAsync(x => x.Status == SettlementRecoveryStatus.Healthy, cancellationToken);
        var noLingeringRaid = raid is null ||
                             (raid.Status is not SettlementRaidStatus.Active and
                                 not SettlementRaidStatus.Scheduled &&
                              !hasSurvivingRaiders);
        var darkwoodRaidReady =
            availableDarkwoodFighters >= WorldPopulationService.AutomaticDarkwoodRaidersRequired &&
            settlementsHealthy &&
            !counterattackActive &&
            noLingeringRaid;
        var counterattackReady =
            faction.DevelopmentStage >= 3 &&
            livingStonehavenResidents >= WorldPopulationService.StonehavenAssaultSoldiersRequired &&
            settlementsHealthy &&
            !counterattackActive &&
            !raidActive &&
            !hasSurvivingRaiders;
        var canStartDarkwoodRaid = isAdministrator && administratorOnline && darkwoodRaidReady;
        var canStartCounterattack = isAdministrator && administratorOnline && counterattackReady;
        var canStartPlaytest = isAdministrator &&
                               administratorOnline &&
                               environment.IsEnvironment("Testing") &&
                               settlementsHealthy &&
                               !counterattackActive &&
                               (raid is null ||
                                raid.Status is not SettlementRaidStatus.Active and
                                    not SettlementRaidStatus.Scheduled &&
                                (raid.Status != SettlementRaidStatus.AttackersWon ||
                                 !hasSurvivingRaiders));
        return new RaidStateResponse(
            raid is not null,
            raidActive || counterattackActive,
            canStartPlaytest,
            darkwoodRaidReady,
            counterattackReady,
            administratorOnline,
            isAdministrator,
            canStartDarkwoodRaid,
            canStartCounterattack,
            raid is null ? null : ToResponse(raid),
            counterattack is null ? null : ToResponse(counterattack),
            CentralClock.Now);
    }

    private static async Task<bool> IsAdministratorOnlineAsync(
        LivingRealmsDbContext database,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Subtract(ActiveAdministratorWindow);
        return await database.PlayerSessions.AsNoTracking()
            .AnyAsync(x => x.CharacterId != null &&
                           x.DisconnectedAt == null &&
                           x.ExpiresAt > now &&
                           x.LastSeenAt != null &&
                           x.LastSeenAt >= cutoff &&
                           x.Account.IsAdministrator,
                cancellationToken);
    }

    private static RaidResponse ToResponse(SettlementRaid raid) => new(
        raid.Id,
        raid.Status.ToString(),
        raid.Phase.ToString(),
        raid.PhaseRound,
        raid.WorldDay,
        raid.ScheduledAt,
        raid.StartedAt,
        raid.LastAdvancedAt,
        raid.ResolvedAt,
        raid.InitialAttackerStrength,
        raid.AttackerStrength,
        raid.InitialDefenderStrength,
        raid.DefenderStrength,
        raid.InitialStructureStrength,
        raid.StructureStrength,
        raid.PlayerContribution,
        raid.SettlementDamage,
        raid.ResidentCasualties,
        raid.ResidentInjuries,
        raid.OutcomeSummary,
        raid.Attackers
            .OrderBy(x => x.Creature.Name)
            .Select(x => new RaidAttackerResponse(
                x.CreatureId,
                x.Creature.Name,
                x.Creature.Title,
                x.Creature.Level,
                x.Creature.Health,
                x.Creature.MaximumHealth,
                x.Creature.Status.ToString(),
                x.IsDefeated,
                x.DefeatedByCharacterId is not null))
            .ToArray());

    private static StonehavenCounterattackResponse ToResponse(StonehavenAssault assault) => new(
        assault.Id,
        assault.Status.ToString(),
        assault.WorldDay,
        assault.StartedAt,
        assault.LastAdvancedAt,
        assault.ResolvedAt,
        assault.InitialSoldierCount,
        assault.SoldiersRemaining,
        assault.InitialGoblinCount,
        assault.GoblinsRemaining,
        assault.CampLevelBefore,
        assault.CampLevelAfter,
        assault.InitialCampStrength,
        assault.CampStrength,
        assault.StonehavenCasualties,
        assault.DarkwoodCasualties,
        assault.OutcomeSummary,
        assault.Members
            .OrderBy(x => x.Resident.Name)
            .Select(x => new StonehavenCounterattackMemberResponse(
                x.ResidentId,
                x.Resident.Name,
                x.Resident.Role,
                x.Resident.Health,
                x.Resident.MaximumHealth,
                x.Resident.Status.ToString(),
                x.IsDefeated))
            .ToArray());

    private static async Task<bool> HasSelectedCharacterAsync(
        HttpContext context,
        LivingRealmsDbContext database)
    {
        var accountId = GetRequiredId(context.User, ClaimTypes.NameIdentifier);
        var sessionId = GetRequiredId(context.User, SessionAuthenticationHandler.SessionIdClaim);
        return await database.PlayerSessions.AnyAsync(
            x => x.Id == sessionId && x.AccountId == accountId && x.CharacterId != null,
            context.RequestAborted);
    }

    private static Guid GetRequiredId(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"The authenticated session is missing {claimType}.");
    }

    private sealed record ErrorResponse(string Error);
    public sealed record RaidStateResponse(
        bool HasRaid,
        bool Active,
        bool CanStartPlaytest,
        bool DarkwoodRaidReady,
        bool StonehavenCounterattackReady,
        bool AdministratorOnline,
        bool IsAdministrator,
        bool CanStartDarkwoodRaid,
        bool CanStartCounterattack,
        RaidResponse? Raid,
        StonehavenCounterattackResponse? Counterattack,
        DateTimeOffset ServerTimeCentral);
    public sealed record RaidResponse(
        Guid Id,
        string Status,
        string Phase,
        int PhaseRound,
        int WorldDay,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? LastAdvancedAt,
        DateTimeOffset? ResolvedAt,
        int InitialAttackerStrength,
        int AttackerStrength,
        int InitialDefenderStrength,
        int DefenderStrength,
        int InitialStructureStrength,
        int StructureStrength,
        int PlayerContribution,
        int SettlementDamage,
        int ResidentCasualties,
        int ResidentInjuries,
        string? OutcomeSummary,
        IReadOnlyCollection<RaidAttackerResponse> Attackers);
    public sealed record RaidAttackerResponse(
        Guid CreatureId,
        string Name,
        string? Title,
        int Level,
        int Health,
        int MaximumHealth,
        string Status,
        bool IsDefeated,
        bool DefeatedByPlayer);
    public sealed record StonehavenCounterattackResponse(
        Guid Id,
        string Status,
        int WorldDay,
        DateTimeOffset StartedAt,
        DateTimeOffset? LastAdvancedAt,
        DateTimeOffset? ResolvedAt,
        int InitialSoldierCount,
        int SoldiersRemaining,
        int InitialGoblinCount,
        int GoblinsRemaining,
        int CampLevelBefore,
        int CampLevelAfter,
        int InitialCampStrength,
        int CampStrength,
        int StonehavenCasualties,
        int DarkwoodCasualties,
        string? OutcomeSummary,
        IReadOnlyCollection<StonehavenCounterattackMemberResponse> Members);
    public sealed record StonehavenCounterattackMemberResponse(
        Guid ResidentId,
        string Name,
        string Role,
        int Health,
        int MaximumHealth,
        string Status,
        bool IsDefeated);
}
