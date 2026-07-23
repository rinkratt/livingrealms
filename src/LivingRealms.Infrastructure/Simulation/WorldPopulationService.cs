using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed class WorldPopulationService(LivingRealmsDbContext database)
{
    public const int StartingStonehavenPopulation = 8;
    public const int StartingDarkwoodPopulation = 7;
    public const int AutomaticDarkwoodRaidersRequired = 15;
    public const int StonehavenAssaultSoldiersRequired = 20;

    private static readonly string[] StonehavenNames =
    [
        "Aveline Hart", "Cedric Vale", "Ysabel Reed", "Garran Holt", "Tamsin Bell",
        "Owyn March", "Linette Fen", "Beric Thorn", "Catrin Webb", "Edric Shaw",
        "Maela Voss", "Tobin Wren", "Ansel Pike", "Rosamund Hale", "Corwin Dyer",
        "Elspeth Wynn", "Harlan Beck", "Isolde Crane", "Perrin Ashe", "Sabine Ford",
        "Lucan Grey", "Meryn Cole", "Osric Dale", "Adela Finch", "Bastian Rook",
        "Cecily Marr", "Darian Frost", "Eveline Moor", "Fenn Ward", "Gisela Lark",
        "Hadrian Moss", "Iona Greer", "Joryn Flint", "Kiera Snow", "Leofric Dunn",
        "Melisande Crow", "Nolan Birch", "Odette Fair", "Piers Rowan", "Rhea Blythe",
        "Stellan Fox", "Thalia West", "Ulric Stone", "Verena Brook", "Wystan Locke",
        "Anwen Frost", "Branoc Mead", "Cerys Hume", "Doran Quill", "Eira North",
        "Falken Wood", "Gretta Low", "Hollis Keen", "Ilse May", "Jareth Pruitt",
        "Katla Sloane", "Lorcan Hurst", "Mirelle Dane", "Niall Kemp", "Orla Gant",
        "Roderic Ames", "Sela Brand", "Torren Cade", "Una Drew", "Varric Ellis",
        "Winifred Fane", "Ysolde Garth", "Aldous Heron", "Bryn Ives", "Clarice Joss",
        "Emeric Kest", "Freya Lang", "Godwin Mott", "Helena Norr", "Idris Orme"
    ];

    private static readonly string[] StonehavenRoleCycle =
    [
        "Stonehaven Guard", "Stonehaven Guard", "Stonehaven Guard", "Stonehaven Guard",
        "Farmer", "Farmer", "Farmer", "Farmer", "Farmer",
        "Carpenter", "Carpenter", "Mason", "Mason", "Hunter", "Hunter", "Weaver", "Weaver",
        "Baker", "Fisher", "Tanner", "Brewer", "Stablehand", "Herbalist", "Scribe", "Potter"
    ];

    private static readonly string[] DarkwoodNames =
    [
        "Krug", "Zarra", "Mog", "Rikka", "Thrak", "Nib", "Grash", "Vexa",
        "Snag", "Hruk", "Yaga", "Drub", "Ketta", "Marn", "Ugri", "Snik",
        "Brakka", "Torg", "Zik", "Grom", "Ruzza", "Krek", "Vorn", "Draz"
    ];

    private static readonly (float X, float Z)[] DarkwoodCampPosts =
    [
        (-4, 3), (3, 4), (10, 0), (9, 10), (-7, 12), (-13, 0), (-12, -10), (4, 13),
        (13, -4), (-14, 6), (13, 7), (-6, -14), (7, -13), (0, 14), (-14, -5), (14, 2),
        (-10, 11), (11, -10), (-2, 11), (8, 12), (-12, 8), (12, -7), (-8, -12), (2, -14)
    ];

    public async Task EnsureStonehavenResidentsAsync(
        bool saveChanges = true,
        CancellationToken cancellationToken = default)
    {
        var settlement = await database.Settlements
            .SingleAsync(x => x.Id == LivingRealmsDbContext.StonehavenVillageId, cancellationToken);
        var existing = await database.SettlementResidents
            .Where(x => x.SettlementId == settlement.Id)
            .ToListAsync(cancellationToken);
        var livingCount = existing.Count(x =>
            x.Health > 0 && x.Status is ResidentStatus.Active or ResidentStatus.Injured);
        var knownNames = existing.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var needed = Math.Max(0, settlement.Population - livingCount);

        // Named residents who were away at the beginning return before new
        // families are materialized. Dead residents remain in the Chronicle;
        // population growth creates a different named person in their place.
        foreach (var returning in existing
                     .Where(x => x.Status == ResidentStatus.Missing)
                     .OrderBy(x => x.CreatedAt)
                     .ThenBy(x => x.Name)
                     .Take(needed))
        {
            returning.Status = ResidentStatus.Active;
            returning.Health = returning.MaximumHealth;
            returning.UpdatedAt = DateTimeOffset.UtcNow;
            needed--;
        }

        for (var index = 0; index < StonehavenNames.Length && needed > 0; index++)
        {
            var name = StonehavenNames[index];
            if (!knownNames.Add(name))
            {
                continue;
            }

            database.SettlementResidents.Add(CreateStonehavenResident(index, name));
            needed--;
        }

        if (needed > 0)
        {
            throw new InvalidOperationException("The Stonehaven resident catalog cannot satisfy the settlement population.");
        }
        if (saveChanges && database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task EnsureDarkwoodClanMembersAsync(
        bool saveChanges = true,
        CancellationToken cancellationToken = default)
    {
        var faction = await database.Factions
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DarkwoodClanId, cancellationToken);
        var allMembers = await database.Creatures
            .Where(x => x.FactionId == faction.Id)
            .ToListAsync(cancellationToken);
        var members = allMembers
            .Where(x => x.Status != CreatureStatus.Retired)
            .ToList();
        var needed = Math.Max(0, faction.Population - members.Count);
        if (needed == 0)
        {
            return;
        }

        var species = await database.CreatureSpecies
            .SingleAsync(x => x.Id == LivingRealmsDbContext.GoblinRaiderSpeciesId, cancellationToken);
        var knownNames = allMembers.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdAt = DateTimeOffset.UtcNow;
        for (var index = 0; index < DarkwoodNames.Length && needed > 0; index++)
        {
            var name = DarkwoodNames[index];
            if (!knownNames.Add(name))
            {
                continue;
            }

            var post = DarkwoodCampPosts[index % DarkwoodCampPosts.Length];
            var level = Math.Max(3, 3 + faction.DevelopmentStage + index % 3);
            var maximumHealth = species.BaseHealth + Math.Max(0, level - 5) * 9;
            var role = (index % 6) switch
            {
                0 => "Clan Raider",
                1 => "Clan Hunter",
                2 => "Woodcutter",
                3 => "Stone Gatherer",
                4 => "Camp Guard",
                _ => "Scout"
            };
            var creature = new Creature
            {
                Id = Guid.Parse($"74000000-0000-4000-8000-{index + 1:000000000000}"),
                SpeciesId = species.Id,
                FactionId = faction.Id,
                RegionId = LivingRealmsDbContext.StonehavenValleyId,
                Name = name,
                Role = role,
                Title = role,
                Level = level,
                Health = maximumHealth,
                MaximumHealth = maximumHealth,
                Attack = species.BaseAttack + Math.Max(0, level - 5) * 2,
                Defense = species.BaseDefense + Math.Max(0, level - 5),
                MovementSpeed = species.BaseMovementSpeed,
                PositionX = -116 + post.X,
                PositionY = 0.08f,
                PositionZ = -104 + post.Z,
                SpawnX = -116 + post.X,
                SpawnY = 0.08f,
                SpawnZ = -104 + post.Z,
                Aggression = role is "Camp Guard" or "Clan Raider" ? 70 : 52,
                Status = CreatureStatus.Alive,
                LastProcessedAt = createdAt,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            creature.Skills.Add(new CreatureSkill
            {
                SkillKey = role switch
                {
                    "Clan Hunter" => "goblin-archery",
                    "Woodcutter" => "woodcutting",
                    "Stone Gatherer" => "stone-gathering",
                    "Scout" => "scouting",
                    _ => "goblin-blade"
                },
                Level = Math.Max(1, level - 2),
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
            database.Creatures.Add(creature);
            needed--;
        }

        if (needed > 0)
        {
            throw new InvalidOperationException("The Darkwood member catalog cannot satisfy the faction population.");
        }
        if (saveChanges)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static SettlementResident CreateStonehavenResident(int index, string name)
    {
        var role = StonehavenRoleCycle[index % StonehavenRoleCycle.Length];
        var canFight = role is "Stonehaven Guard" or "Hunter";
        var maximumHealth = role switch
        {
            "Stonehaven Guard" => 110,
            "Hunter" => 100,
            "Mason" or "Carpenter" or "Farmer" => 95,
            _ => 85
        };
        var column = index % 15;
        var row = index / 15;
        var homeX = -25.0f + column * 3.55f;
        var homeZ = -31.0f + row * 5.4f;
        var work = ResolveStonehavenWorkPosition(index, role);
        var safeX = -8.0f + index % 9 * 2.0f;
        var safeZ = -18.0f + index / 9 % 5 * 1.7f;
        var createdAt = DateTimeOffset.UtcNow;
        return new SettlementResident
        {
            Id = Guid.Parse($"73000000-0000-4000-8000-{index + 1:000000000000}"),
            SettlementId = LivingRealmsDbContext.StonehavenVillageId,
            Name = name,
            Role = role,
            Health = maximumHealth,
            MaximumHealth = maximumHealth,
            Status = ResidentStatus.Active,
            CanFight = canFight,
            HomeX = homeX,
            HomeY = 0.08f,
            HomeZ = homeZ,
            WorkX = work.X,
            WorkY = 0.08f,
            WorkZ = work.Z,
            SafeX = safeX,
            SafeY = 0.08f,
            SafeZ = safeZ,
            Dialogue = DialogueFor(role),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private static (float X, float Z) ResolveStonehavenWorkPosition(int index, string role) => role switch
    {
        "Stonehaven Guard" => (index % 4) switch
        {
            0 => (-23 + index % 3 * 3, -33),
            1 => (23 - index % 3 * 3, -33),
            2 => (-26, -4 - index % 5 * 5),
            _ => (26, -4 - index % 5 * 5)
        },
        "Farmer" => (-24 + index % 9 * 6, 10 + index / 9 % 3 * 6),
        "Carpenter" => (-20 + index % 3 * 2, -18 + index % 2 * 3),
        "Mason" => (20 + index % 3 * 2, -19 + index % 2 * 3),
        "Hunter" => (-35 + index % 5 * 17, 25 + index % 3 * 8),
        "Weaver" => (-15 + index % 4 * 2, -8),
        "Baker" => (8 + index % 3 * 2, -9),
        "Fisher" => (36 + index % 4 * 3, 8 + index % 5 * 5),
        "Tanner" => (-21 + index % 3 * 2, -26),
        "Brewer" => (13 + index % 3 * 2, -17),
        "Stablehand" => (21 + index % 3 * 2, -27),
        "Herbalist" => (-14 + index % 3 * 2, -25),
        "Scribe" => (0 + index % 3 * 2, -14),
        "Potter" => (16 + index % 3 * 2, -24),
        _ => (-10 + index % 11 * 2, -12 - index % 5 * 3)
    };

    private static string DialogueFor(string role) => role switch
    {
        "Stonehaven Guard" => "My watch is marked on the roster. I know the wall section and the neighbors entrusted to me.",
        "Farmer" => "Stonehaven eats because these fields are worked in every season, not because food appears in a storehouse.",
        "Carpenter" => "A straight beam and a sound joint will outlast any hurried patchwork.",
        "Mason" => "Every fitted stone carries part of Stonehaven's defense.",
        "Hunter" => "I read tracks beyond the wall and bring warning home before trouble reaches the gate.",
        "Weaver" => "Cloth, cord, bandages, and winter wool all begin at my loom.",
        "Baker" => "The ovens start before sunrise so every worker can carry bread into the day.",
        "Fisher" => "The river provides, but only for those who understand its current.",
        "Tanner" => "Nothing from a hunt should be wasted—not hide, sinew, or bone.",
        "Brewer" => "Clean water and a careful barrel keep spirits up and sickness down.",
        "Stablehand" => "A settlement moves at the pace of the animals it cares for.",
        "Herbalist" => "The valley grows medicine beside poison. Skill is knowing which is which.",
        "Scribe" => "Names, stores, debts, births, and losses belong in the record.",
        "Potter" => "A good vessel keeps grain dry, water clean, and medicine safe.",
        _ => "Stonehaven is our home, and every pair of hands has work to do."
    };
}
