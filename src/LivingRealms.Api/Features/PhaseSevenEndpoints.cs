using System.Security.Claims;
using LivingRealms.Api.Security;
using LivingRealms.Api.Time;
using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Api.Features;

public static class PhaseSevenEndpoints
{
    public static IEndpointRouteBuilder MapPhaseSevenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/regions/stonehaven-valley/residents", ListResidentsAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListResidentsAsync(
        HttpContext context,
        LivingRealmsDbContext database,
        WorldPopulationService population)
    {
        if (!await HasSelectedCharacterAsync(context, database))
        {
            return Results.Conflict(new ErrorResponse(
                "Select a character before entering Stonehaven Village."));
        }

        await population.EnsureStonehavenResidentsAsync(cancellationToken: context.RequestAborted);
        var simulatedHours = await database.Factions.AsNoTracking()
            .Where(x => x.Id == LivingRealmsDbContext.DarkwoodClanId)
            .Select(x => x.SimulatedHours)
            .SingleAsync(context.RequestAborted);
        var worldHour = (int)(simulatedHours % 24);
        var worldDay = (int)(simulatedHours / 24) + 1;
        var raidActive = await database.SettlementRaids.AsNoTracking()
            .AnyAsync(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                           x.Status == SettlementRaidStatus.Active,
                context.RequestAborted);
        var activeAssault = await database.StonehavenAssaults.AsNoTracking()
            .Include(x => x.Members)
            .Where(x => x.Status == StonehavenAssaultStatus.Assembling ||
                        x.Status == StonehavenAssaultStatus.Marching ||
                        x.Status == StonehavenAssaultStatus.FightingGoblins ||
                        x.Status == StonehavenAssaultStatus.AttackingCamp)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(context.RequestAborted);
        var assaultMemberIndexes = activeAssault?.Members
            .OrderBy(x => x.ResidentId)
            .Select((member, index) => new { member.ResidentId, Index = index })
            .ToDictionary(x => x.ResidentId, x => x.Index) ?? [];
        var residents = await database.SettlementResidents.AsNoTracking()
            .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId)
            .OrderByDescending(x => x.CanFight)
            .ThenBy(x => x.Role)
            .ThenBy(x => x.Name)
            .ToListAsync(context.RequestAborted);

        var response = residents.Select(resident =>
        {
            var isAssaultMember = assaultMemberIndexes.TryGetValue(resident.Id, out var assaultIndex);
            var schedule = ResolveSchedule(
                resident,
                worldHour,
                raidActive,
                isAssaultMember ? activeAssault?.Status : null,
                assaultIndex);
            return new ResidentResponse(
                resident.Id,
                resident.Name,
                resident.Role,
                resident.Health,
                resident.MaximumHealth,
                resident.Status.ToString(),
                resident.CanFight || isAssaultMember,
                SkillsFor(resident.Role),
                resident.PrimarySkill,
                resident.SkillLevel,
                resident.Trait,
                resident.Experience,
                resident.IsMajor,
                resident.MemorySummary,
                schedule.Activity,
                schedule.Position,
                ToPosition(resident.HomeX, resident.HomeY, resident.HomeZ),
                ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ),
                ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ),
                resident.Dialogue,
                worldHour,
                worldDay,
                CentralClock.Now);
        }).ToArray();

        return Results.Ok(response);
    }

    private static ResidentSchedule ResolveSchedule(
        SettlementResident resident,
        int worldHour,
        bool raidActive,
        StonehavenAssaultStatus? assaultStatus,
        int assaultIndex)
    {
        if (resident.Status == ResidentStatus.Dead)
        {
            return new("Fallen", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ));
        }
        if (resident.Status == ResidentStatus.Missing)
        {
            return new("Missing", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ));
        }
        if (assaultStatus is not null)
        {
            return assaultStatus.Value switch
            {
                StonehavenAssaultStatus.Assembling =>
                    new("Assembling for the Darkwood counterattack", AssaultFormation(assaultIndex, -5, 8, 2.0f)),
                StonehavenAssaultStatus.Marching =>
                    new("Marching on Darkwood", AssaultFormation(assaultIndex, -64, -48, 2.0f)),
                StonehavenAssaultStatus.FightingGoblins =>
                    new("Fighting Darkwood's goblins", AssaultRing(assaultIndex, 14.0f)),
                StonehavenAssaultStatus.AttackingCamp =>
                    new("Destroying the Darkwood camp", AssaultRing(assaultIndex, 7.5f)),
                _ => new("Returning from Darkwood", ToPosition(resident.HomeX, resident.HomeY, resident.HomeZ))
            };
        }
        if (raidActive)
        {
            return resident.Role switch
            {
                "Guard Captain" or "Stonehaven Guard" or "Militia Recruit" =>
                    new("Defending Stonehaven", ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ)),
                "Reeve of Stonehaven" =>
                    new("Coordinating Stonehaven's defense", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Blacksmith" =>
                    new("Holding the reserve line", ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ)),
                "Healer" =>
                    new("Tending wounded defenders", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Innkeeper" =>
                    new("Sheltering townsfolk at the inn", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Storekeeper" =>
                    new("Securing emergency supplies", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Lumberjack" =>
                    new("Barricading the gate", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Quarry Worker" or "Iron Miner" =>
                    new("Reinforcing the walls", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                "Villager" =>
                    new("Carrying water and messages", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ)),
                _ when resident.CanFight =>
                    new("Defending Stonehaven", ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ)),
                _ =>
                    new("Sheltering from the raid", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ))
            };
        }

        if (resident.Status == ResidentStatus.Injured || resident.Health < resident.MaximumHealth / 2)
        {
            return new("Recovering", ToPosition(resident.SafeX, resident.SafeY, resident.SafeZ));
        }

        if (resident.Role == "A3 Mine Guard")
        {
            var patrolStep = (worldHour + StablePatrolOffset(resident.Id)) % 3;
            var patrol = patrolStep switch
            {
                0 => ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ),
                1 => ToPosition(resident.WorkX - 5, resident.WorkY, resident.WorkZ + 4),
                _ => ToPosition(resident.WorkX + 5, resident.WorkY, resident.WorkZ + 4)
            };
            return new("Protecting Irondeep Mine", patrol);
        }

        if (resident.Role.Contains("Guard", StringComparison.OrdinalIgnoreCase))
        {
            if (worldHour is >= 6 and < 22)
            {
                var patrolStep = (worldHour + StablePatrolOffset(resident.Id)) % 4;
                var patrol = patrolStep switch
                {
                    0 => ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ),
                    1 => ToPosition(-8, resident.WorkY, -3),
                    2 => ToPosition(8, resident.WorkY, -3),
                    _ => ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ)
                };
                return new("Patrolling Stonehaven", patrol);
            }
            return new("Guarding the gate", ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ));
        }

        var working = resident.Role switch
        {
            "Innkeeper" => worldHour is >= 6 and < 23,
            "Villager" => worldHour is >= 7 and < 20,
            "Lumberjack" or "Quarry Worker" or "Iron Miner" => true,
            _ => worldHour is >= 7 and < 19
        };
        return working
            ? new($"Working as {resident.Role.ToLowerInvariant()}", ToPosition(resident.WorkX, resident.WorkY, resident.WorkZ))
            : new("Resting at home", ToPosition(resident.HomeX, resident.HomeY, resident.HomeZ));
    }

    private static PositionResponse AssaultFormation(
        int index,
        float originX,
        float originZ,
        float spacing) =>
        ToPosition(
            originX + (index % 5 - 2) * spacing,
            0.08f,
            originZ + (index / 5) * spacing);

    private static PositionResponse AssaultRing(int index, float radius)
    {
        var angle = index * MathF.Tau / WorldPopulationService.StonehavenAssaultSoldiersRequired;
        return ToPosition(
            -116.0f + MathF.Cos(angle) * radius,
            0.08f,
            -104.0f + MathF.Sin(angle) * radius);
    }

    private static int StablePatrolOffset(Guid id) => Math.Abs(id.GetHashCode() % 4);

    private static IReadOnlyCollection<string> SkillsFor(string role) => role switch
    {
        "Guard Captain" => ["Command", "Swordsmanship", "Shield Defense", "Tactics"],
        "Reeve of Stonehaven" => ["Administration", "Diplomacy", "Provisioning", "Law"],
        "Stonehaven Guard" => ["Swordsmanship", "Shield Defense", "Patrol"],
        "Militia Recruit" => ["Swordsmanship", "Shield Defense", "Local Knowledge"],
        "Blacksmith" => ["Blacksmithing", "Weapon Repair", "Reserve Defense"],
        "Innkeeper" => ["Hospitality", "Cooking", "Rumorcraft"],
        "Healer" => ["Medicine", "Herbalism", "Triage"],
        "Storekeeper" => ["Trade", "Appraisal", "Provisioning"],
        "Lumberjack" => ["Woodcutting", "Forestry", "Carpentry"],
        "Quarry Worker" => ["Quarrying", "Stonecutting", "Masonry"],
        "Iron Miner" => ["Iron Mining", "Ore Hauling", "Prospecting"],
        "Farmer" => ["Farming", "Animal Handling", "Seedcraft"],
        "Carpenter" => ["Carpentry", "Joinery", "Repair"],
        "Mason" => ["Masonry", "Fortification", "Quarrying"],
        "Hunter" => ["Archery", "Tracking", "Fieldcraft"],
        "Weaver" => ["Weaving", "Tailoring", "Dyeing"],
        "Baker" => ["Baking", "Hearthcraft", "Provisioning"],
        "Fisher" => ["Fishing", "Netmaking", "Rivercraft"],
        "Tanner" => ["Tanning", "Leatherworking", "Skinning"],
        "Brewer" => ["Brewing", "Foraging", "Trade"],
        "Stablehand" => ["Animal Handling", "Riding", "Farriery"],
        "Herbalist" => ["Herbalism", "First Aid", "Foraging"],
        "Scribe" => ["Literacy", "Recordkeeping", "Lore"],
        "Potter" => ["Pottery", "Kilncraft", "Clay Gathering"],
        _ => ["Labor", "Local Knowledge", "Self Defense"]
    };

    private static PositionResponse ToPosition(float x, float y, float z) => new(x, y, z);

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

    private sealed record ResidentSchedule(string Activity, PositionResponse Position);
    private sealed record ErrorResponse(string Error);
    public sealed record PositionResponse(float X, float Y, float Z);
    public sealed record ResidentResponse(
        Guid Id,
        string Name,
        string Role,
        int Health,
        int MaximumHealth,
        string Status,
        bool CanFight,
        IReadOnlyCollection<string> Skills,
        string PrimarySkill,
        int SkillLevel,
        string Trait,
        long Experience,
        bool IsMajor,
        string MemorySummary,
        string Activity,
        PositionResponse Position,
        PositionResponse HomePosition,
        PositionResponse WorkPosition,
        PositionResponse SafePosition,
        string Dialogue,
        int WorldHour,
        int WorldDay,
        DateTimeOffset ServerTimeCentral);
}
