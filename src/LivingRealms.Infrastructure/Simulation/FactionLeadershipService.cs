using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed class FactionLeadershipService(LivingRealmsDbContext database)
{
    public async Task<FactionDefeatResult> ResolvePersistentDefeatAsync(
        Creature defeated,
        DateTimeOffset defeatedAt,
        Guid? characterId = null,
        bool adjustPopulation = true,
        CancellationToken cancellationToken = default)
    {
        if (defeated.FactionId is null)
        {
            defeated.Status = CreatureStatus.Dead;
            defeated.RespawnAt = defeatedAt.AddSeconds(Math.Max(15, defeated.Species.RespawnSeconds));
            return new(false, null, null);
        }

        var faction = await database.Factions
            .SingleAsync(x => x.Id == defeated.FactionId.Value, cancellationToken);
        var wasLeader = faction.LeaderCreatureId == defeated.Id;
        defeated.Status = wasLeader ? CreatureStatus.Dead : CreatureStatus.Retired;
        defeated.RespawnAt = null;
        defeated.Health = 0;
        defeated.LastProcessedAt = defeatedAt;
        defeated.UpdatedAt = defeatedAt;
        if (adjustPopulation)
        {
            faction.Population = Math.Max(0, faction.Population - 1);
        }
        faction.MilitaryStrength = Math.Max(0, faction.MilitaryStrength - Math.Max(8, defeated.Level * 2));
        faction.UpdatedAt = defeatedAt;

        if (!wasLeader)
        {
            database.WorldHistory.Add(new WorldHistory
            {
                EventType = "persistent_faction_member_fell",
                Title = $"{defeated.Name} fell and did not return",
                Description =
                    $"{defeated.Name}, {defeated.Title ?? defeated.Role ?? "a member of the faction"}, was permanently lost. " +
                    "The faction must replace that person from its living population.",
                RegionId = defeated.RegionId,
                FactionId = faction.Id,
                CreatureId = defeated.Id,
                CharacterId = characterId,
                OccurredAt = defeatedAt,
                ImportanceLevel = 2,
                CreatedAt = defeatedAt,
                UpdatedAt = defeatedAt
            });
            return new(false, null, null);
        }

        faction.Morale = Math.Max(0, faction.Morale - 12);
        var successor = await database.Creatures
            .Where(x => x.FactionId == faction.Id &&
                        x.Id != defeated.Id &&
                        x.Status == CreatureStatus.Alive &&
                        x.Health > 0 &&
                        x.Role != "Raid Attacker")
            .OrderByDescending(x => x.Leadership)
            .ThenByDescending(x => x.Level)
            .ThenByDescending(x => x.Experience)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (successor is null)
        {
            faction.LeaderCreatureId = null;
            database.WorldHistory.Add(new WorldHistory
            {
                EventType = "faction_leader_fell",
                Title = $"{defeated.Name} fell and Darkwood entered a succession crisis",
                Description =
                    $"{defeated.Name} was permanently defeated. No living candidate could immediately claim leadership.",
                RegionId = defeated.RegionId,
                FactionId = faction.Id,
                CreatureId = defeated.Id,
                CharacterId = characterId,
                OccurredAt = defeatedAt,
                ImportanceLevel = 5,
                CreatedAt = defeatedAt,
                UpdatedAt = defeatedAt
            });
            return new(true, null, $"{defeated.Name} fell; the faction has no leader.");
        }

        successor.Role = "Chief";
        successor.Title = faction.DevelopmentStage >= 3 && successor.Level >= 12
            ? "Goblin Warlord"
            : faction.DevelopmentStage >= 2 && successor.Level >= 9
                ? "Goblin Chieftain"
                : "Goblin Chief";
        successor.Leadership = Math.Max(successor.Leadership, 8 + faction.DevelopmentStage * 3);
        successor.Experience += Math.Max(20, defeated.Level * 5L);
        successor.UpdatedAt = defeatedAt;
        faction.LeaderCreatureId = successor.Id;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "faction_leadership_succession",
            Title = $"{successor.Name} succeeded {defeated.Name}",
            Description =
                $"{defeated.Name} was permanently defeated. The surviving Darkwood clan elevated " +
                $"{successor.Name} to {successor.Title}; the new leader inherited a weakened and angry faction.",
            RegionId = defeated.RegionId,
            FactionId = faction.Id,
            CreatureId = successor.Id,
            CharacterId = characterId,
            OccurredAt = defeatedAt,
            ImportanceLevel = 5,
            CreatedAt = defeatedAt,
            UpdatedAt = defeatedAt
        });
        return new(
            true,
            successor,
            $"{defeated.Name} fell permanently. {successor.Name} became {successor.Title}.");
    }

    public async Task<Creature?> EnsureLeaderAsync(
        Faction faction,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken = default)
    {
        if (faction.LeaderCreatureId is not null)
        {
            var current = await database.Creatures.SingleOrDefaultAsync(
                x => x.Id == faction.LeaderCreatureId.Value &&
                     x.Status == CreatureStatus.Alive &&
                     x.Health > 0,
                cancellationToken);
            if (current is not null)
            {
                return current;
            }
        }

        var candidate = await database.Creatures
            .Where(x => x.FactionId == faction.Id &&
                        x.Status == CreatureStatus.Alive &&
                        x.Health > 0 &&
                        x.Role != "Raid Attacker")
            .OrderByDescending(x => x.Leadership)
            .ThenByDescending(x => x.Level)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            faction.LeaderCreatureId = null;
            return null;
        }

        candidate.Role = "Chief";
        candidate.Title = "Goblin Chief";
        candidate.Leadership = Math.Max(candidate.Leadership, 8);
        candidate.UpdatedAt = decidedAt;
        faction.LeaderCreatureId = candidate.Id;
        faction.UpdatedAt = decidedAt;
        return candidate;
    }
}

public sealed record FactionDefeatResult(
    bool LeadershipChanged,
    Creature? Successor,
    string? ChronicleSummary);
