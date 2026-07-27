using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LivingRealms.Infrastructure.Simulation;

public sealed partial class RaidSimulationService(
    LivingRealmsDbContext database,
    WorldPopulationService population,
    FactionLeadershipService leadership,
    WorldStructureService structures,
    SettlementRecoveryService recovery,
    ILogger<RaidSimulationService> logger)
{
    private static readonly TimeSpan OnlineRaidRoundInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ActivePlayerWindow = TimeSpan.FromMinutes(2);
    private const int CampaignMarchRounds = 4;
    private static readonly (float X, float Y, float Z)[] RaidSpawns =
    [
        (-124.0f, 0.08f, -96.0f),
        (-119.0f, 0.08f, -94.0f),
        (-113.0f, 0.08f, -94.0f),
        (-108.0f, 0.08f, -96.0f),
        (-127.0f, 0.08f, -99.0f),
        (-122.0f, 0.08f, -99.0f),
        (-117.0f, 0.08f, -99.0f),
        (-112.0f, 0.08f, -99.0f),
        (-105.0f, 0.08f, -99.0f),
        (-127.0f, 0.08f, -103.0f),
        (-122.0f, 0.08f, -103.0f),
        (-117.0f, 0.08f, -103.0f),
        (-112.0f, 0.08f, -103.0f),
        (-107.0f, 0.08f, -103.0f),
        (-102.0f, 0.08f, -103.0f)
    ];
    private static readonly (float X, float Y, float Z)[] RaidMarchWaypoints =
    [
        (-98.0f, 0.08f, -98.0f),
        (-96.0f, 0.08f, 10.0f),
        (-42.0f, 0.08f, 12.0f),
        (-12.0f, 0.08f, 11.0f)
    ];

    private static bool IsActive(StonehavenAssaultStatus status) =>
        status is StonehavenAssaultStatus.Assembling or
            StonehavenAssaultStatus.Marching or
            StonehavenAssaultStatus.FightingGoblins or
            StonehavenAssaultStatus.AttackingCamp;

    public async Task<SettlementRaid> StartRaidAsync(
        DateTimeOffset startedAt,
        string source,
        CancellationToken cancellationToken = default)
    {
        var existing = await database.SettlementRaids
            .Include(x => x.Attackers)
            .ThenInclude(x => x.Creature)
            .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                        (x.Status == SettlementRaidStatus.Scheduled ||
                         x.Status == SettlementRaidStatus.Active ||
                         (x.Status == SettlementRaidStatus.AttackersWon &&
                          x.Attackers.Any(attacker =>
                              !attacker.IsDefeated &&
                              attacker.Creature.Status == CreatureStatus.Alive &&
                              attacker.Creature.Health > 0))))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (await database.SettlementRecoveries.AsNoTracking()
                .AnyAsync(x => x.Status != SettlementRecoveryStatus.Healthy, cancellationToken))
        {
            throw new InvalidOperationException(
                "No campaign can begin while Stonehaven or Darkwood is defeated or rebuilding.");
        }

        await population.EnsureDarkwoodClanMembersAsync(cancellationToken: cancellationToken);
        await population.EnsureStonehavenResidentsAsync(cancellationToken: cancellationToken);

        var activeAssault = await database.StonehavenAssaults.AsNoTracking()
            .AnyAsync(x => x.Status == StonehavenAssaultStatus.Assembling ||
                           x.Status == StonehavenAssaultStatus.Marching ||
                           x.Status == StonehavenAssaultStatus.FightingGoblins ||
                           x.Status == StonehavenAssaultStatus.AttackingCamp,
                cancellationToken);
        if (activeAssault)
        {
            throw new InvalidOperationException("Stonehaven is already marching on Darkwood.");
        }

        var faction = await database.Factions
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var leader = await leadership.EnsureLeaderAsync(faction, startedAt, cancellationToken)
            ?? throw new InvalidOperationException("Darkwood has no living leader capable of ordering a raid.");
        var settlement = await database.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        var availableMembers = await database.Creatures
            .Where(x => x.FactionId == faction.Id &&
                        x.Status == CreatureStatus.Alive &&
                        x.Health > 0 &&
                        x.Role != "Raid Attacker" &&
                        x.Id != leader.Id)
            .ToListAsync(cancellationToken);
        var developmentPlaytest = source.Equals("development-control", StringComparison.OrdinalIgnoreCase);
        var raidSize = developmentPlaytest
            ? 4
            : WorldPopulationService.AutomaticDarkwoodRaidersRequired;
        var raidMembers = availableMembers
            .OrderBy(x => x.Title switch
            {
                "Clan Raider" => 0,
                "Camp Guard" => 1,
                "Clan Hunter" => 2,
                "Scout" => 3,
                _ => 4
            })
            .ThenBy(x => x.Name)
            .Take(raidSize)
            .ToList();
        if (raidMembers.Count < raidSize)
        {
            throw new InvalidOperationException(
                $"Darkwood does not have {raidSize} available clan members for this raid.");
        }

        if (!developmentPlaytest && ShouldLeaderJoinRaid(leader, faction))
        {
            raidMembers.Add(leader);
        }

        var attackerStrength = 0;
        var defenderStrength = await database.SettlementResidents
            .Where(x => x.SettlementId == settlement.Id &&
                        x.CanFight &&
                        x.Health > 0 &&
                        (x.Status == ResidentStatus.Active || x.Status == ResidentStatus.Injured))
            .SumAsync(x => x.Health, cancellationToken);
        var structureStrength = await structures.GetRemainingHealthAsync(
            ResourceOwner.Stonehaven,
            cancellationToken);
        var raid = new SettlementRaid
        {
            SettlementId = settlement.Id,
            AttackingFactionId = faction.Id,
            Status = SettlementRaidStatus.Active,
            Phase = DarkwoodRaidPhase.Assembling,
            PhaseRound = 0,
            WorldDay = (int)(faction.SimulatedHours / 24) + 1,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            LastAdvancedAt = startedAt,
            InitialAttackerStrength = attackerStrength,
            AttackerStrength = attackerStrength,
            InitialDefenderStrength = defenderStrength,
            DefenderStrength = defenderStrength,
            InitialStructureStrength = structureStrength,
            StructureStrength = structureStrength,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        };
        database.SettlementRaids.Add(raid);

        for (var index = 0; index < raidMembers.Count; index++)
        {
            var spawn = RaidSpawns[index % RaidSpawns.Length];
            var creature = raidMembers[index];
            attackerStrength += creature.Health;
            creature.Title ??= creature.Role;
            creature.Role = "Raid Attacker";
            creature.PositionX = spawn.X;
            creature.PositionY = spawn.Y;
            creature.PositionZ = spawn.Z;
            creature.Aggression = Math.Max(90, creature.Aggression);
            creature.LastProcessedAt = startedAt;
            creature.UpdatedAt = startedAt;
            database.SettlementRaidAttackers.Add(new SettlementRaidAttacker
            {
                Raid = raid,
                Creature = creature,
                CreatedAt = startedAt,
                UpdatedAt = startedAt
            });
        }

        raid.InitialAttackerStrength = attackerStrength;
        raid.AttackerStrength = attackerStrength;
        raid.InitialDefenderStrength = defenderStrength;
        raid.DefenderStrength = defenderStrength;

        settlement.LastAttackedAt = startedAt;
        settlement.UpdatedAt = startedAt;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "stonehaven_raid_begun",
            Title = "The Darkwood war horn sounded",
            Description = $"{raidMembers.Count} raid-ready Darkwood fighters answered {leader.Name}'s war horn on world day {raid.WorldDay}. They must assemble, march to Stonehaven, defeat its defenders, and physically destroy its structures before Darkwood can claim victory.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = faction.Id,
            CreatureId = leader.Id,
            OccurredAt = startedAt,
            ImportanceLevel = 5,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        LogRaidStarted(logger, raid.Id, raid.WorldDay, source, CentralNow());
        return raid;
    }

    public async Task<SettlementRaid?> AdvanceActiveRaidAsync(
        DateTimeOffset advancedAt,
        int rounds = 1,
        bool ignoreMinimumInterval = false,
        CancellationToken cancellationToken = default)
    {
        var raid = await LoadActiveRaidAsync(cancellationToken);
        if (raid is null)
        {
            return null;
        }
        if (!ignoreMinimumInterval && raid.LastAdvancedAt is not null &&
            advancedAt < raid.LastAdvancedAt.Value.Add(OnlineRaidRoundInterval))
        {
            return raid;
        }
        if (!ignoreMinimumInterval && raid.StartedAt is not null &&
            advancedAt < raid.StartedAt.Value.AddSeconds(15))
        {
            return raid;
        }

        rounds = Math.Clamp(rounds, 1, 24);
        for (var round = 0; round < rounds && raid.Status == SettlementRaidStatus.Active; round++)
        {
            var livingAttackers = GetLivingAttackers(raid.Attackers);
            if (livingAttackers.Length == 0)
            {
                raid.AttackerStrength = 0;
                await ResolveRaidAsync(raid, defendersWon: true, advancedAt, cancellationToken);
                break;
            }

            if (raid.Phase == DarkwoodRaidPhase.Assembling)
            {
                raid.Phase = DarkwoodRaidPhase.Marching;
                raid.PhaseRound = 0;
                continue;
            }

            if (raid.Phase == DarkwoodRaidPhase.Marching)
            {
                MoveRaidFormation(raid.Attackers, raid.PhaseRound, advancedAt);
                raid.PhaseRound++;
                if (raid.PhaseRound >= CampaignMarchRounds)
                {
                    raid.Phase = DarkwoodRaidPhase.FightingDefenders;
                    raid.PhaseRound = 0;
                }
                continue;
            }

            if (raid.Phase == DarkwoodRaidPhase.FightingDefenders)
            {
                var livingDefenders = GetFrontLineDefenders(raid.Settlement.Residents);
                if (livingDefenders.Length == 0)
                {
                    raid.DefenderStrength = 0;
                    raid.Phase = DarkwoodRaidPhase.AttackingStructures;
                    raid.PhaseRound = 0;
                    continue;
                }

                DamageAttackers(
                    livingDefenders,
                    raid.Attackers,
                    raid.Settlement.WeaponTier,
                    advancedAt);
                livingAttackers = GetLivingAttackers(raid.Attackers);
                if (livingAttackers.Length > 0)
                {
                    raid.ResidentCasualties += DamageDefenders(
                        livingAttackers,
                        raid.Settlement.Residents,
                        raid.Settlement.ArmorTier,
                        advancedAt);
                    ApplySettlementSupport(raid.Settlement.Residents, advancedAt);
                }

                raid.AttackerStrength = GetLivingAttackers(raid.Attackers).Sum(x => x.Creature.Health);
                raid.DefenderStrength = GetLivingDefenders(raid.Settlement.Residents).Sum(x => x.Health);
                if (raid.AttackerStrength == 0)
                {
                    await ResolveRaidAsync(raid, defendersWon: true, advancedAt, cancellationToken);
                    break;
                }
                if (raid.DefenderStrength == 0)
                {
                    raid.Phase = DarkwoodRaidPhase.AttackingStructures;
                    raid.PhaseRound = 0;
                }
                continue;
            }

            if (raid.Phase == DarkwoodRaidPhase.AttackingStructures)
            {
                var structureDamage = livingAttackers.Sum(attacker =>
                    Math.Max(8, attacker.Creature.Attack));
                var impact = await structures.DamageOwnerAsync(
                    ResourceOwner.Stonehaven,
                    structureDamage,
                    advancedAt,
                    cancellationToken);
                raid.PhaseRound++;
                raid.SettlementDamage += impact.DamageApplied;
                raid.StructureStrength = impact.OwnerHealthRemaining;
                if (raid.StructureStrength == 0)
                {
                    await ResolveRaidAsync(raid, defendersWon: false, advancedAt, cancellationToken);
                    break;
                }
            }
        }

        raid.LastAdvancedAt = advancedAt;
        raid.UpdatedAt = advancedAt;
        await database.SaveChangesAsync(cancellationToken);
        return raid;
    }

    public async Task AdvanceActiveConflictAsync(
        DateTimeOffset advancedAt,
        int rounds = 1,
        bool ignoreMinimumInterval = false,
        CancellationToken cancellationToken = default)
    {
        var raidActive = await database.SettlementRaids.AsNoTracking()
            .AnyAsync(x => x.Status == SettlementRaidStatus.Active, cancellationToken);
        if (raidActive)
        {
            await AdvanceActiveRaidAsync(advancedAt, rounds, ignoreMinimumInterval, cancellationToken);
            return;
        }

        await AdvanceActiveStonehavenAssaultAsync(
            advancedAt,
            rounds,
            ignoreMinimumInterval,
            cancellationToken);
    }

    public async Task<RaidContributionResult?> RegisterPlayerDefeatAsync(
        Creature creature,
        Guid characterId,
        DateTimeOffset defeatedAt,
        CancellationToken cancellationToken = default)
    {
        var attacker = await database.SettlementRaidAttackers
            .Include(x => x.Raid)
            .ThenInclude(x => x.Settlement)
            .ThenInclude(x => x.Residents)
            .Include(x => x.Raid)
            .ThenInclude(x => x.AttackingFaction)
            .ThenInclude(x => x.Resources)
            .Include(x => x.Raid)
            .ThenInclude(x => x.Attackers)
            .ThenInclude(x => x.Creature)
            .SingleOrDefaultAsync(x => x.CreatureId == creature.Id &&
                                       (x.Raid.Status == SettlementRaidStatus.Active ||
                                        x.Raid.Status == SettlementRaidStatus.AttackersWon),
                cancellationToken);
        if (attacker is null || attacker.IsDefeated)
        {
            return null;
        }

        var raid = attacker.Raid;
        var fellAfterBreach = raid.Status == SettlementRaidStatus.AttackersWon;
        var contribution = Math.Max(1, creature.MaximumHealth);
        RetireAttacker(attacker, defeatedAt, characterId);
        if (fellAfterBreach)
        {
            raid.AttackingFaction.Population = Math.Max(1, raid.AttackingFaction.Population - 1);
            raid.AttackingFaction.MilitaryStrength = Math.Max(10, raid.AttackingFaction.MilitaryStrength - 8);
            raid.AttackingFaction.UpdatedAt = defeatedAt;
        }
        raid.PlayerContribution += contribution;
        raid.AttackerStrength = GetLivingAttackers(raid.Attackers).Sum(x => x.Creature.Health);
        raid.UpdatedAt = defeatedAt;
        if (raid.AttackerStrength == 0 && raid.Status == SettlementRaidStatus.Active)
        {
            await ResolveRaidAsync(raid, defendersWon: true, defeatedAt, cancellationToken);
        }
        else if (raid.AttackerStrength == 0 && raid.Status == SettlementRaidStatus.AttackersWon)
        {
            raid.OutcomeSummary =
                $"Darkwood breached Stonehaven, but players later defeated every surviving raider. " +
                $"The village suffered {raid.SettlementDamage} structural damage, {raid.ResidentInjuries} injuries, and {raid.ResidentCasualties} death(s).";
            database.WorldHistory.Add(new WorldHistory
            {
                EventType = "stonehaven_raid_aftermath_cleared",
                Title = "The last Darkwood raider fell in Stonehaven",
                Description = raid.OutcomeSummary,
                RegionId = LivingRealmsDbContext.StonehavenValleyId,
                FactionId = raid.AttackingFactionId,
                CharacterId = characterId,
                OccurredAt = defeatedAt,
                ImportanceLevel = 4,
                CreatedAt = defeatedAt,
                UpdatedAt = defeatedAt
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        return new RaidContributionResult(raid.Id, contribution, raid.PlayerContribution, raid.Status);
    }

    public async Task<StonehavenAssault> StartStonehavenAssaultAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await database.StonehavenAssaults
            .Include(x => x.Members)
            .ThenInclude(x => x.Resident)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.Status == StonehavenAssaultStatus.Assembling ||
                                      x.Status == StonehavenAssaultStatus.Marching ||
                                      x.Status == StonehavenAssaultStatus.FightingGoblins ||
                                      x.Status == StonehavenAssaultStatus.AttackingCamp,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (await database.SettlementRecoveries.AsNoTracking()
                .AnyAsync(x => x.Status != SettlementRecoveryStatus.Healthy, cancellationToken))
        {
            throw new InvalidOperationException(
                "No campaign can begin while Stonehaven or Darkwood is defeated or rebuilding.");
        }

        if (await database.SettlementRaids.AsNoTracking()
                .AnyAsync(x => x.Status == SettlementRaidStatus.Active, cancellationToken))
        {
            throw new InvalidOperationException("Stonehaven cannot march while Darkwood is attacking the village.");
        }

        await population.EnsureStonehavenResidentsAsync(cancellationToken: cancellationToken);
        await population.EnsureDarkwoodClanMembersAsync(cancellationToken: cancellationToken);
        var faction = await database.Factions
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        if (faction.DevelopmentStage < 3)
        {
            throw new InvalidOperationException("Stonehaven only launches its counterattack against a completed level 3 Darkwood camp.");
        }

        var settlement = await database.Settlements
            .Include(x => x.Residents)
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        var soldiers = settlement.Residents
            .Where(x => x.Health > 0 && x.Status is ResidentStatus.Active or ResidentStatus.Injured)
            .OrderByDescending(x => x.Role == "Guard Captain")
            .ThenByDescending(x => x.Role == "Stonehaven Guard")
            .ThenByDescending(x => x.CanFight)
            .ThenByDescending(x => x.Health)
            .ThenBy(x => x.Name)
            .Take(WorldPopulationService.StonehavenAssaultSoldiersRequired)
            .ToArray();
        if (soldiers.Length < WorldPopulationService.StonehavenAssaultSoldiersRequired)
        {
            throw new InvalidOperationException(
                $"Stonehaven needs {WorldPopulationService.StonehavenAssaultSoldiersRequired} living residents before it can assemble the counterattack.");
        }

        var goblinCount = await database.Creatures.AsNoTracking()
            .CountAsync(x => x.FactionId == faction.Id &&
                             x.Status == CreatureStatus.Alive &&
                             x.Health > 0,
                cancellationToken);
        var campStrength = await structures.GetRemainingHealthAsync(
            ResourceOwner.Darkwood,
            cancellationToken);
        if (campStrength <= 0)
        {
            throw new InvalidOperationException(
                "Darkwood has no standing camp structures for Stonehaven to attack.");
        }
        var assault = new StonehavenAssault
        {
            SettlementId = settlement.Id,
            DefendingFactionId = faction.Id,
            Status = StonehavenAssaultStatus.Assembling,
            PhaseRound = 0,
            WorldDay = (int)(faction.SimulatedHours / 24) + 1,
            StartedAt = startedAt,
            LastAdvancedAt = startedAt,
            InitialSoldierCount = soldiers.Length,
            SoldiersRemaining = soldiers.Length,
            InitialGoblinCount = goblinCount,
            GoblinsRemaining = goblinCount,
            CampLevelBefore = faction.DevelopmentStage,
            CampLevelAfter = faction.DevelopmentStage,
            InitialCampStrength = campStrength,
            CampStrength = campStrength,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        };
        database.StonehavenAssaults.Add(assault);
        foreach (var soldier in soldiers)
        {
            database.StonehavenAssaultMembers.Add(new StonehavenAssaultMember
            {
                Assault = assault,
                Resident = soldier,
                CreatedAt = startedAt,
                UpdatedAt = startedAt
            });
        }

        database.WorldHistory.Add(new WorldHistory
        {
            EventType = "stonehaven_counterattack_begun",
            Title = "Guard Captain Mira mustered twenty for Darkwood",
            Description = $"Darkwood completed its level {faction.DevelopmentStage} fortified camp. Guard Captain Mira assembled {soldiers.Length} named Stonehaven soldiers and militia to march on the camp, defeat its goblin defenders, and tear it back down a level.",
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = faction.Id,
            OccurredAt = startedAt,
            ImportanceLevel = 5,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        LogCounterattackStarted(logger, assault.Id, assault.WorldDay, soldiers.Length, CentralNow());
        return assault;
    }

    public async Task<StonehavenAssault?> AdvanceActiveStonehavenAssaultAsync(
        DateTimeOffset advancedAt,
        int rounds = 1,
        bool ignoreMinimumInterval = false,
        CancellationToken cancellationToken = default)
    {
        var assault = await LoadActiveStonehavenAssaultAsync(cancellationToken);
        if (assault is null)
        {
            return null;
        }
        if (!ignoreMinimumInterval && assault.LastAdvancedAt is not null &&
            advancedAt < assault.LastAdvancedAt.Value.Add(OnlineRaidRoundInterval))
        {
            return assault;
        }

        var goblins = await database.Creatures
            .Where(x => x.FactionId == assault.DefendingFactionId)
            .ToListAsync(cancellationToken);
        rounds = Math.Clamp(rounds, 1, 24);
        for (var round = 0; round < rounds && IsActive(assault.Status); round++)
        {
            if (assault.Status == StonehavenAssaultStatus.Assembling)
            {
                assault.PhaseRound++;
                if (assault.PhaseRound >= 2)
                {
                    assault.Status = StonehavenAssaultStatus.Marching;
                    assault.PhaseRound = 0;
                }
                continue;
            }
            if (assault.Status == StonehavenAssaultStatus.Marching)
            {
                assault.PhaseRound++;
                if (assault.PhaseRound >= CampaignMarchRounds)
                {
                    assault.Status = StonehavenAssaultStatus.FightingGoblins;
                    assault.PhaseRound = 0;
                }
                continue;
            }

            var soldiers = LivingAssaultMembers(assault.Members);
            if (soldiers.Length == 0)
            {
                await ResolveStonehavenAssaultAsync(assault, stonehavenWon: false, goblins, advancedAt, cancellationToken);
                break;
            }

            var defenders = LivingDarkwoodDefenders(
                goblins,
                assault.DefendingFaction.LeaderCreatureId);
            if (assault.Status == StonehavenAssaultStatus.FightingGoblins)
            {
                if (defenders.Length == 0)
                {
                    assault.Status = StonehavenAssaultStatus.AttackingCamp;
                    assault.GoblinsRemaining = 0;
                    continue;
                }

                DamageDarkwoodDefenders(
                    soldiers,
                    goblins,
                    assault.DefendingFaction.LeaderCreatureId,
                    assault.Settlement.WeaponTier,
                    advancedAt);
                defenders = LivingDarkwoodDefenders(
                    goblins,
                    assault.DefendingFaction.LeaderCreatureId);
                if (defenders.Length > 0)
                {
                    DamageStonehavenSoldiers(
                        defenders,
                        assault,
                        assault.Settlement.ArmorTier,
                        advancedAt);
                }
                assault.SoldiersRemaining = LivingAssaultMembers(assault.Members).Length;
                assault.GoblinsRemaining = LivingDarkwoodDefenders(
                    goblins,
                    assault.DefendingFaction.LeaderCreatureId).Length;
                assault.DarkwoodCasualties = Math.Max(0,
                    assault.InitialGoblinCount - assault.GoblinsRemaining);
                if (assault.SoldiersRemaining == 0)
                {
                    await ResolveStonehavenAssaultAsync(assault, stonehavenWon: false, goblins, advancedAt, cancellationToken);
                    break;
                }
                if (assault.GoblinsRemaining == 0)
                {
                    assault.Status = StonehavenAssaultStatus.AttackingCamp;
                }
            }
            else if (assault.Status == StonehavenAssaultStatus.AttackingCamp)
            {
                var campDamage = LivingAssaultMembers(assault.Members).Sum(member =>
                    Math.Max(5, (SoldierAttackPower(member.Resident) +
                                 assault.Settlement.WeaponTier * 3) / 2));
                var structureDamage = await structures.DamageOwnerAsync(
                    ResourceOwner.Darkwood,
                    campDamage,
                    advancedAt,
                    cancellationToken);
                assault.CampStrength = structureDamage.OwnerHealthRemaining;
                if (assault.CampStrength == 0)
                {
                    await ResolveStonehavenAssaultAsync(assault, stonehavenWon: true, goblins, advancedAt, cancellationToken);
                    break;
                }
            }
        }

        assault.LastAdvancedAt = advancedAt;
        assault.UpdatedAt = advancedAt;
        await database.SaveChangesAsync(cancellationToken);
        return assault;
    }

    public async Task EvaluateWorldProgressionAsync(
        int worldHours,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        var activeRaid = await database.SettlementRaids.AsNoTracking()
            .AnyAsync(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                           x.Status == SettlementRaidStatus.Active,
                cancellationToken);
        var activeAssault = await database.StonehavenAssaults.AsNoTracking()
            .AnyAsync(x => x.Status == StonehavenAssaultStatus.Assembling ||
                           x.Status == StonehavenAssaultStatus.Marching ||
                           x.Status == StonehavenAssaultStatus.FightingGoblins ||
                           x.Status == StonehavenAssaultStatus.AttackingCamp,
                cancellationToken);
        if (activeRaid || activeAssault)
        {
            var hasActivePlayer = await database.PlayerSessions.AsNoTracking()
                .AnyAsync(session =>
                        session.CharacterId != null &&
                        session.DisconnectedAt == null &&
                        session.ExpiresAt > processedAt &&
                        session.LastSeenAt != null &&
                        session.LastSeenAt >= processedAt.Subtract(ActivePlayerWindow),
                    cancellationToken);
            await AdvanceActiveConflictAsync(
                processedAt,
                hasActivePlayer
                    ? 1
                    : Math.Clamp(Math.Max(1, worldHours / 2), 1, 24),
                ignoreMinimumInterval: !hasActivePlayer,
                cancellationToken);
            return;
        }

        await population.EnsureDarkwoodClanMembersAsync(cancellationToken: cancellationToken);
        await population.EnsureStonehavenResidentsAsync(cancellationToken: cancellationToken);
        // Readiness is exposed by the world and raid endpoints. A battle only
        // enters its active state after an authenticated administrator presses
        // the matching authorization control in the game client.
    }

    private async Task<StonehavenAssault?> LoadActiveStonehavenAssaultAsync(
        CancellationToken cancellationToken) =>
        await database.StonehavenAssaults
            .Include(x => x.Settlement)
            .Include(x => x.DefendingFaction)
            .ThenInclude(x => x.Structures)
            .Include(x => x.Members)
            .ThenInclude(x => x.Resident)
            .Where(x => x.Status == StonehavenAssaultStatus.Assembling ||
                        x.Status == StonehavenAssaultStatus.Marching ||
                        x.Status == StonehavenAssaultStatus.FightingGoblins ||
                        x.Status == StonehavenAssaultStatus.AttackingCamp)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static StonehavenAssaultMember[] LivingAssaultMembers(
        IEnumerable<StonehavenAssaultMember> members) =>
        members
            .Where(x => !x.IsDefeated &&
                        x.Resident.Health > 0 &&
                        x.Resident.Status is ResidentStatus.Active or ResidentStatus.Injured)
            .OrderBy(x => x.Resident.Name)
            .ToArray();

    private static Creature[] LivingDarkwoodDefenders(
        IEnumerable<Creature> goblins,
        Guid? leaderCreatureId) =>
        goblins
            .Where(x => x.Status == CreatureStatus.Alive && x.Health > 0)
            .OrderBy(x => x.Id == leaderCreatureId)
            .ThenBy(x => x.Health)
            .ThenBy(x => x.Name)
            .ToArray();

    private static int SoldierAttackPower(SettlementResident resident) => resident.Role switch
    {
        "Guard Captain" => 18,
        "Stonehaven Guard" => 15,
        "Blacksmith" or "Hunter" => 13,
        "Lumberjack" or "Quarry Worker" or "Iron Miner" or "Mason" => 11,
        _ => 9
    };

    private static void DamageDarkwoodDefenders(
        StonehavenAssaultMember[] soldiers,
        IEnumerable<Creature> goblins,
        Guid? leaderCreatureId,
        int weaponTier,
        DateTimeOffset damagedAt)
    {
        for (var index = 0; index < soldiers.Length; index++)
        {
            var defenders = LivingDarkwoodDefenders(goblins, leaderCreatureId);
            if (defenders.Length == 0)
            {
                break;
            }

            var target = defenders[index % defenders.Length];
            var damage = Math.Max(
                3,
                SoldierAttackPower(soldiers[index].Resident) + weaponTier * 3 - target.Defense / 4);
            target.Health = Math.Max(0, target.Health - damage);
            target.UpdatedAt = damagedAt;
            target.LastProcessedAt = damagedAt;
            if (target.Health == 0)
            {
                target.Status = target.Id == leaderCreatureId
                    ? CreatureStatus.Dead
                    : CreatureStatus.Retired;
                target.RespawnAt = null;
            }
        }
    }

    private static void DamageStonehavenSoldiers(
        Creature[] goblins,
        StonehavenAssault assault,
        int armorTier,
        DateTimeOffset damagedAt)
    {
        for (var index = 0; index < goblins.Length; index++)
        {
            var soldiers = LivingAssaultMembers(assault.Members);
            if (soldiers.Length == 0)
            {
                break;
            }

            var target = soldiers[index % soldiers.Length];
            var damage = Math.Max(3, goblins[index].Attack / 2 - armorTier * 2);
            target.Resident.Health = Math.Max(0, target.Resident.Health - damage);
            target.Resident.UpdatedAt = damagedAt;
            if (target.Resident.Health == 0)
            {
                target.Resident.Status = ResidentStatus.Dead;
                target.IsDefeated = true;
                target.DefeatedAt = damagedAt;
                target.UpdatedAt = damagedAt;
                assault.StonehavenCasualties++;
                assault.Settlement.Population = Math.Max(0, assault.Settlement.Population - 1);
            }
            else
            {
                target.Resident.Status = target.Resident.Health < target.Resident.MaximumHealth / 2
                    ? ResidentStatus.Injured
                    : ResidentStatus.Active;
            }
        }
    }

    private async Task ResolveStonehavenAssaultAsync(
        StonehavenAssault assault,
        bool stonehavenWon,
        IReadOnlyCollection<Creature> goblins,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        if (!IsActive(assault.Status))
        {
            return;
        }

        assault.Status = stonehavenWon
            ? StonehavenAssaultStatus.StonehavenVictory
            : StonehavenAssaultStatus.DarkwoodVictory;
        assault.ResolvedAt = resolvedAt;
        assault.SoldiersRemaining = LivingAssaultMembers(assault.Members).Length;
        assault.GoblinsRemaining = LivingDarkwoodDefenders(
            goblins,
            assault.DefendingFaction.LeaderCreatureId).Length;
        assault.DarkwoodCasualties = Math.Max(0,
            assault.InitialGoblinCount - assault.GoblinsRemaining);

        if (stonehavenWon)
        {
            var faction = assault.DefendingFaction;
            faction.DevelopmentStage = Math.Max(1, assault.CampLevelBefore - 1);
            assault.CampLevelAfter = faction.DevelopmentStage;
            faction.PopulationCapacity = faction.DevelopmentStage switch
            {
                1 => 10,
                2 => 16,
                _ => 24
            };
            faction.TechnologyLevel = faction.DevelopmentStage;
            faction.TerritorySize = faction.DevelopmentStage;
            faction.Morale = Math.Max(0, faction.Morale - 20);
            faction.Aggression = Math.Max(20, faction.Aggression - 10);

            var destroyedStructures = faction.Structures
                .Where(x => faction.DevelopmentStage < 3 &&
                            x.StructureType is "Darkwood Watchtower" or "Iron Workshop")
                .ToArray();
            if (destroyedStructures.Length > 0)
            {
                database.FactionStructures.RemoveRange(destroyedStructures);
            }

            var palisade = await database.ConstructionProjects
                .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodPalisadeProjectId,
                    cancellationToken);
            palisade.CurrentLevel = Math.Max(0, palisade.CurrentLevel - 1);
            palisade.WoodContributed = 0;
            palisade.StoneContributed = 0;
            palisade.CompletedAt = null;
            palisade.UpdatedAt = resolvedAt;

            var defeatedLeader = faction.LeaderCreatureId is null
                ? null
                : goblins.SingleOrDefault(x =>
                    x.Id == faction.LeaderCreatureId.Value &&
                    (x.Status != CreatureStatus.Alive || x.Health <= 0));
            FactionDefeatResult? succession = null;
            if (defeatedLeader is not null)
            {
                succession = await leadership.ResolvePersistentDefeatAsync(
                    defeatedLeader,
                    resolvedAt,
                    adjustPopulation: false,
                    cancellationToken: cancellationToken);
            }

            var currentLeader = await leadership.EnsureLeaderAsync(
                faction,
                resolvedAt,
                cancellationToken);
            if (defeatedLeader is null && currentLeader is not null)
            {
                currentLeader.Health = Math.Max(1, currentLeader.MaximumHealth / 4);
                currentLeader.PositionX = currentLeader.SpawnX;
                currentLeader.PositionY = currentLeader.SpawnY;
                currentLeader.PositionZ = currentLeader.SpawnZ;
                currentLeader.RespawnAt = null;
                currentLeader.UpdatedAt = resolvedAt;
                currentLeader.LastProcessedAt = resolvedAt;
            }

            faction.Population = goblins.Count(x =>
                x.Status == CreatureStatus.Alive && x.Health > 0);
            faction.MilitaryStrength = Math.Max(0,
                faction.Population * 6 +
                (currentLeader?.Level ?? 0) * 8 +
                faction.DevelopmentStage * 25);
            faction.UpdatedAt = resolvedAt;
            var leaderOutcome = succession?.ChronicleSummary ??
                                (currentLeader is null
                                    ? "Darkwood was left without a leader."
                                    : $"{currentLeader.Name} escaped wounded.");
            await recovery.MarkDefeatedAsync(
                ResourceOwner.Darkwood,
                resolvedAt,
                cancellationToken);
            assault.CampLevelAfter = 0;
            assault.OutcomeSummary =
                $"Stonehaven's {assault.InitialSoldierCount} fighters cleared Darkwood's defenders and completely destroyed the fortified camp. " +
                $"Darkwood is defeated for fifteen real minutes before its {WorldPopulationService.StartingDarkwoodPopulation} founders return and rebuild functional structures before the palisade. " +
                $"{assault.SoldiersRemaining} Stonehaven fighters returned. {leaderOutcome}";
        }
        else
        {
            assault.CampLevelAfter = assault.CampLevelBefore;
            assault.DefendingFaction.Morale = Math.Min(100, assault.DefendingFaction.Morale + 10);
            assault.DefendingFaction.UpdatedAt = resolvedAt;
            assault.Settlement.DefenseRating = Math.Max(10, assault.Settlement.DefenseRating - 4);
            assault.OutcomeSummary =
                $"Darkwood defeated Stonehaven's counterattack. {assault.StonehavenCasualties} of the {assault.InitialSoldierCount} soldiers and militia fell; " +
                $"the level {assault.CampLevelBefore} camp remained standing with {assault.GoblinsRemaining} goblin defender(s).";
        }

        assault.Settlement.UpdatedAt = resolvedAt;
        assault.UpdatedAt = resolvedAt;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = stonehavenWon
                ? "stonehaven_counterattack_won"
                : "stonehaven_counterattack_lost",
            Title = stonehavenWon
                ? "Stonehaven tore down Darkwood's fortified camp"
                : "Darkwood broke Stonehaven's counterattack",
            Description = assault.OutcomeSummary,
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = assault.DefendingFactionId,
            OccurredAt = resolvedAt,
            ImportanceLevel = 5,
            CreatedAt = resolvedAt,
            UpdatedAt = resolvedAt
        });
        LogCounterattackResolved(
            logger,
            assault.Id,
            assault.Status.ToString(),
            assault.SoldiersRemaining,
            CentralNow());
    }

    private async Task<SettlementRaid?> LoadActiveRaidAsync(CancellationToken cancellationToken) =>
        await database.SettlementRaids
            .Include(x => x.Settlement)
            .ThenInclude(x => x.Residents)
            .Include(x => x.AttackingFaction)
            .ThenInclude(x => x.Resources)
            .Include(x => x.Attackers)
            .ThenInclude(x => x.Creature)
            .Where(x => x.SettlementId == LivingRealmsDbContext.StonehavenVillageId &&
                        x.Status == SettlementRaidStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task ResolveRaidAsync(
        SettlementRaid raid,
        bool defendersWon,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        if (raid.Status != SettlementRaidStatus.Active)
        {
            return;
        }

        raid.Status = defendersWon ? SettlementRaidStatus.DefendersWon : SettlementRaidStatus.AttackersWon;
        raid.Phase = DarkwoodRaidPhase.Resolved;
        raid.ResolvedAt = resolvedAt;
        if (defendersWon)
        {
            foreach (var attacker in raid.Attackers.Where(x => !x.IsDefeated))
            {
                WithdrawAttacker(
                    attacker,
                    resolvedAt,
                    attacker.CreatureId == raid.AttackingFaction.LeaderCreatureId);
            }
        }

        var defeatedAttackers = raid.Attackers.Count(x => x.IsDefeated);
        raid.AttackingFaction.Population = Math.Max(1, raid.AttackingFaction.Population - defeatedAttackers);
        raid.AttackingFaction.MilitaryStrength = Math.Max(10, raid.AttackingFaction.MilitaryStrength - defeatedAttackers * 8);

        if (defendersWon)
        {
            raid.SettlementDamage += Math.Clamp(
                raid.InitialDefenderStrength - raid.DefenderStrength,
                0,
                80);
            raid.Settlement.StructuralIntegrity = Math.Max(0,
                raid.Settlement.StructuralIntegrity - raid.SettlementDamage);
            raid.AttackingFaction.Morale = Math.Max(0, raid.AttackingFaction.Morale - 6);
            raid.ResidentInjuries = raid.Settlement.Residents.Count(x => x.Status == ResidentStatus.Injured);
            raid.OutcomeSummary = raid.PlayerContribution > 0
                ? $"Stonehaven repelled the persistent campaign. Players contributed {raid.PlayerContribution} strength; the village suffered {raid.SettlementDamage} damage and lost {raid.ResidentCasualties} defender(s)."
                : $"Stonehaven's guards repelled the persistent campaign, suffered {raid.SettlementDamage} damage, and lost {raid.ResidentCasualties} defender(s).";
        }
        else
        {
            raid.SettlementDamage = Math.Max(raid.SettlementDamage, 240);
            raid.Settlement.StructuralIntegrity = Math.Max(0,
                raid.Settlement.StructuralIntegrity - raid.SettlementDamage);
            raid.Settlement.DefenseRating = Math.Max(10, raid.Settlement.DefenseRating - 8);
            raid.Settlement.GuardStrength = Math.Max(5, raid.Settlement.GuardStrength - 10);
            raid.Settlement.Food = Math.Max(0, raid.Settlement.Food - 80);
            raid.Settlement.Wood = Math.Max(0, raid.Settlement.Wood - 40);
            raid.Settlement.Iron = Math.Max(0, raid.Settlement.Iron - 10);
            raid.ResidentCasualties += ApplyCivilianConsequences(raid.Settlement.Residents, resolvedAt);
            raid.ResidentInjuries = raid.Settlement.Residents.Count(x => x.Status == ResidentStatus.Injured);
            raid.AttackingFaction.Morale = Math.Min(100, raid.AttackingFaction.Morale + 8);
            var gold = raid.AttackingFaction.Resources.SingleOrDefault(x => x.Kind == ResourceKind.Gold);
            if (gold is not null)
            {
                gold.Amount = Math.Min(gold.Capacity, gold.Amount + 20);
            }
            var survivingRaiders = GetLivingAttackers(raid.Attackers).Length;
            raid.OutcomeSummary =
                $"Darkwood fought through Stonehaven's defenders and reduced its built structures from {raid.InitialStructureStrength} to {raid.StructureStrength} health, " +
                $"causing {raid.SettlementDamage} settlement damage, " +
                $"injured {raid.ResidentInjuries}, and killed {raid.ResidentCasualties} resident(s). " +
                $"{survivingRaiders} surviving raider(s) remain in the village until players defeat them or the world is reset.";
        }

        raid.Settlement.Population = Math.Max(0, raid.Settlement.Population - raid.ResidentCasualties);
        raid.Settlement.IsDestroyed = raid.Settlement.StructuralIntegrity <= 0;
        if (!defendersWon)
        {
            await recovery.MarkDefeatedAsync(
                ResourceOwner.Stonehaven,
                resolvedAt,
                cancellationToken);
            raid.OutcomeSummary +=
                $" Stonehaven is defeated for fifteen real minutes before its {WorldPopulationService.StartingStonehavenPopulation} founders return to rebuild functional structures before gates and walls.";
        }
        raid.Settlement.UpdatedAt = resolvedAt;
        raid.AttackingFaction.UpdatedAt = resolvedAt;
        raid.UpdatedAt = resolvedAt;
        database.WorldHistory.Add(new WorldHistory
        {
            EventType = defendersWon ? "stonehaven_raid_repelled" : "stonehaven_raid_lost",
            Title = defendersWon ? "Stonehaven held the northern gate" : "Darkwood breached Stonehaven",
            Description = raid.OutcomeSummary,
            RegionId = LivingRealmsDbContext.StonehavenValleyId,
            FactionId = raid.AttackingFactionId,
            OccurredAt = resolvedAt,
            ImportanceLevel = 5,
            CreatedAt = resolvedAt,
            UpdatedAt = resolvedAt
        });
        await Task.CompletedTask;
        LogRaidResolved(logger, raid.Id, raid.Status.ToString(), raid.PlayerContribution, CentralNow());
    }

    private static bool ShouldLeaderJoinRaid(Creature leader, Faction faction)
    {
        var worldDay = (int)(faction.SimulatedHours / 24) + 1;
        var roll = (leader.Id.ToByteArray()[0] + worldDay * 17) % 100;
        return roll < 35;
    }

    private static void MoveRaidFormation(
        IEnumerable<SettlementRaidAttacker> attackers,
        int marchRound,
        DateTimeOffset movedAt)
    {
        var waypoint = RaidMarchWaypoints[Math.Clamp(
            marchRound,
            0,
            RaidMarchWaypoints.Length - 1)];
        var living = GetLivingAttackers(attackers);
        for (var index = 0; index < living.Length; index++)
        {
            var lane = index % 5 - 2;
            var rank = index / 5;
            var creature = living[index].Creature;
            creature.PositionX = waypoint.X + lane * 1.6f;
            creature.PositionY = waypoint.Y;
            creature.PositionZ = waypoint.Z - rank * 1.8f;
            creature.LastProcessedAt = movedAt;
            creature.UpdatedAt = movedAt;
        }
    }

    private static void RetireAttacker(
        SettlementRaidAttacker attacker,
        DateTimeOffset defeatedAt,
        Guid? characterId)
    {
        attacker.IsDefeated = true;
        attacker.DefeatedAt = defeatedAt;
        attacker.DefeatedByCharacterId = characterId;
        attacker.UpdatedAt = defeatedAt;
        attacker.Creature.Health = 0;
        attacker.Creature.Status = CreatureStatus.Retired;
        attacker.Creature.RespawnAt = null;
        attacker.Creature.UpdatedAt = defeatedAt;
        attacker.Creature.LastProcessedAt = defeatedAt;
    }

    private static void WithdrawAttacker(
        SettlementRaidAttacker attacker,
        DateTimeOffset withdrawnAt,
        bool isFactionLeader)
    {
        attacker.UpdatedAt = withdrawnAt;
        attacker.Creature.Role = isFactionLeader
            ? "Chief"
            : attacker.Creature.Title ?? "Clan Raider";
        attacker.Creature.PositionX = attacker.Creature.SpawnX;
        attacker.Creature.PositionY = attacker.Creature.SpawnY;
        attacker.Creature.PositionZ = attacker.Creature.SpawnZ;
        attacker.Creature.Status = CreatureStatus.Alive;
        attacker.Creature.RespawnAt = null;
        attacker.Creature.UpdatedAt = withdrawnAt;
        attacker.Creature.LastProcessedAt = withdrawnAt;
    }

    private static SettlementRaidAttacker[] GetLivingAttackers(
        IEnumerable<SettlementRaidAttacker> attackers) =>
        attackers
            .Where(x => !x.IsDefeated &&
                        x.Creature.Status == CreatureStatus.Alive &&
                        x.Creature.Health > 0)
            .OrderBy(x => x.Creature.Name)
            .ToArray();

    private static SettlementResident[] GetLivingDefenders(
        IEnumerable<SettlementResident> residents) =>
        residents
            .Where(x => x.CanFight &&
                        x.Health > 0 &&
                        x.Status is ResidentStatus.Active or ResidentStatus.Injured)
            .OrderBy(x => x.Name)
            .ToArray();

    private static SettlementResident[] GetFrontLineDefenders(
        IEnumerable<SettlementResident> residents)
    {
        var fighters = GetLivingDefenders(residents);
        var guards = fighters
            .Where(x => x.Role.Contains("Guard", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return guards.Length > 0 ? guards : fighters;
    }

    private static void DamageAttackers(
        SettlementResident[] defenders,
        IEnumerable<SettlementRaidAttacker> attackers,
        int weaponTier,
        DateTimeOffset damagedAt)
    {
        var livingAtStart = GetLivingAttackers(attackers).Length;
        var engagedDefenders = defenders.Take(Math.Max(1, livingAtStart)).ToArray();
        for (var index = 0; index < engagedDefenders.Length; index++)
        {
            var livingAttackers = GetLivingAttackers(attackers);
            if (livingAttackers.Length == 0)
            {
                break;
            }

            var target = livingAttackers[index % livingAttackers.Length];
            var damage = engagedDefenders[index].Role switch
            {
                "Guard Captain" => 8,
                "Stonehaven Guard" => 6,
                "Blacksmith" => 5,
                _ => 4
            };
            damage += weaponTier * 2;
            target.Creature.Health = Math.Max(0, target.Creature.Health - damage);
            target.Creature.UpdatedAt = damagedAt;
            target.Creature.LastProcessedAt = damagedAt;
            if (target.Creature.Health == 0)
            {
                RetireAttacker(target, damagedAt, null);
            }
        }
    }

    private static int DamageDefenders(
        SettlementRaidAttacker[] attackers,
        IEnumerable<SettlementResident> residents,
        int armorTier,
        DateTimeOffset damagedAt)
    {
        var deaths = 0;
        for (var index = 0; index < attackers.Length; index++)
        {
            var defenders = GetFrontLineDefenders(residents);
            if (defenders.Length == 0)
            {
                break;
            }

            var defender = defenders[index % defenders.Length];
            var damage = Math.Max(
                Math.Max(3, 8 - armorTier * 2),
                attackers[index].Creature.Attack * 2 / 3 - armorTier * 2);
            defender.Health = Math.Max(0, defender.Health - damage);
            defender.UpdatedAt = damagedAt;
            if (defender.Health == 0)
            {
                defender.Status = ResidentStatus.Dead;
                deaths++;
            }
            else
            {
                defender.Status = defender.Health < defender.MaximumHealth / 2
                    ? ResidentStatus.Injured
                    : ResidentStatus.Active;
            }
        }
        return deaths;
    }

    private static void ApplySettlementSupport(
        IEnumerable<SettlementResident> residents,
        DateTimeOffset supportedAt)
    {
        var residentList = residents.ToArray();
        var healerAvailable = residentList.Any(x =>
            x.Role == "Healer" &&
            x.Health > 0 &&
            x.Status is ResidentStatus.Active or ResidentStatus.Injured);
        if (!healerAvailable)
        {
            return;
        }

        var woundedDefender = GetLivingDefenders(residentList)
            .Where(x => x.Health < x.MaximumHealth)
            .OrderBy(x => x.Health / (double)Math.Max(1, x.MaximumHealth))
            .ThenBy(x => x.Name)
            .FirstOrDefault();
        if (woundedDefender is null)
        {
            return;
        }

        woundedDefender.Health = Math.Min(woundedDefender.MaximumHealth, woundedDefender.Health + 8);
        woundedDefender.Status = woundedDefender.Health < woundedDefender.MaximumHealth / 2
            ? ResidentStatus.Injured
            : ResidentStatus.Active;
        woundedDefender.UpdatedAt = supportedAt;
    }

    private static int ApplyCivilianConsequences(
        IEnumerable<SettlementResident> residents,
        DateTimeOffset occurredAt)
    {
        var civilians = residents
            .Where(x => !x.CanFight && x.Status != ResidentStatus.Dead)
            .OrderBy(x => x.MaximumHealth)
            .ThenBy(x => x.Name)
            .ToArray();
        if (civilians.Length > 0)
        {
            civilians[0].Health = 0;
            civilians[0].Status = ResidentStatus.Dead;
            civilians[0].UpdatedAt = occurredAt;
        }
        if (civilians.Length > 1)
        {
            civilians[1].Health = Math.Max(1, civilians[1].MaximumHealth / 3);
            civilians[1].Status = ResidentStatus.Injured;
            civilians[1].UpdatedAt = occurredAt;
        }
        return civilians.Length > 0 ? 1 : 0;
    }

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
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "Raid {RaidId} started on world day {WorldDay} from {Source} at {CentralTime}")]
    private static partial void LogRaidStarted(
        ILogger logger,
        Guid raidId,
        int worldDay,
        string source,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "Raid {RaidId} resolved as {Outcome} with player contribution {PlayerContribution} at {CentralTime}")]
    private static partial void LogRaidResolved(
        ILogger logger,
        Guid raidId,
        string outcome,
        int playerContribution,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "Stonehaven counterattack {AssaultId} started on world day {WorldDay} with {SoldierCount} soldiers at {CentralTime}")]
    private static partial void LogCounterattackStarted(
        ILogger logger,
        Guid assaultId,
        int worldDay,
        int soldierCount,
        DateTimeOffset centralTime);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "Stonehaven counterattack {AssaultId} resolved as {Outcome} with {SoldiersRemaining} soldiers remaining at {CentralTime}")]
    private static partial void LogCounterattackResolved(
        ILogger logger,
        Guid assaultId,
        string outcome,
        int soldiersRemaining,
        DateTimeOffset centralTime);
}

public sealed record RaidContributionResult(
    Guid RaidId,
    int ContributionGained,
    int TotalPlayerContribution,
    SettlementRaidStatus Status);
