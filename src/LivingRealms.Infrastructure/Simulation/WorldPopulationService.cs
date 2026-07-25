using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed class WorldPopulationService(LivingRealmsDbContext database)
{
    public const int StartingStonehavenPopulation = 8;
    public const int StartingDarkwoodPopulation = 7;
    public const int StonehavenFarmPlotCount = 8;
    public const int StonehavenFarmhouseCount = 2;
    public const int StonehavenHousingCapacity = 24;
    public const int StartingStonehavenFood = 64;
    public const int StartingStonehavenWood = 40;
    public const int StartingStonehavenStone = 24;
    public const int StartingStonehavenIron = 4;
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

    private static readonly string[] StonehavenArrivalRoleCycle =
    [
        "Miner", "Fisher", "Farmer", "Farmer", "Stonehaven Guard",
        "Carpenter", "Carpenter", "Mason", "Mason", "Hunter", "Hunter", "Weaver", "Weaver",
        "Baker", "Fisher", "Tanner", "Brewer", "Stablehand", "Herbalist", "Scribe", "Potter"
    ];

    private static readonly string[] DarkwoodNames =
    [
        "Krug", "Zarra", "Mog", "Rikka", "Thrak", "Nib", "Grash", "Vexa",
        "Snag", "Hruk", "Yaga", "Drub", "Ketta", "Marn", "Ugri", "Snik",
        "Brakka", "Torg", "Zik", "Grom", "Ruzza", "Krek", "Vorn", "Draz"
    ];

    private static readonly string[] DarkwoodEpithets =
    [
        "Ash-Ear", "Red Fang", "Moss-Back", "Crooked Spear", "Black Nail", "Bog-Eye",
        "Split Helm", "Thorn-Foot", "Iron Tooth", "Mud-Cloak", "Crow-Bone", "Night-Ear",
        "Root-Cutter", "Gray Scar", "Wolf-Bait", "Cold Hand", "Flint-Eye", "Briar-Born",
        "Stone Jaw", "Smoke-Snout", "Ragged Ear", "Deep-Claw", "Rotwood", "Moon-Biter"
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

        // Legacy placeholder residents remain in the Chronicle, but population
        // growth reintroduces them in the intentional arrival order. Mara Venn is a
        // story-specific missing resident and does not silently become a new
        // arrival.
        var arrivalNumber = Math.Max(0, livingCount - StartingStonehavenPopulation);
        foreach (var returning in existing
                     .Where(x => x.Status == ResidentStatus.Missing &&
                                 x.Id != LivingRealmsDbContext.MaraVennResidentId)
                     .OrderBy(x => x.CreatedAt)
                     .ThenBy(x => x.Name)
                     .Take(needed))
        {
            var residentIndex = Array.FindIndex(
                StonehavenNames,
                candidate => candidate.Equals(returning.Name, StringComparison.OrdinalIgnoreCase));
            ConfigureStonehavenArrival(
                returning,
                residentIndex >= 0 ? residentIndex : StartingStonehavenPopulation + arrivalNumber,
                StonehavenArrivalRoleCycle[arrivalNumber % StonehavenArrivalRoleCycle.Length]);
            arrivalNumber++;
            needed--;
        }

        for (var index = 0; index < StonehavenNames.Length && needed > 0; index++)
        {
            var name = StonehavenNames[index];
            if (!knownNames.Add(name))
            {
                continue;
            }

            database.SettlementResidents.Add(CreateStonehavenResident(
                index,
                name,
                StonehavenArrivalRoleCycle[arrivalNumber % StonehavenArrivalRoleCycle.Length]));
            arrivalNumber++;
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
            .Where(x => x.Status == CreatureStatus.Alive && x.Health > 0)
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
        var candidateIndex = 0;
        while (candidateIndex < DarkwoodNames.Length * 100 && needed > 0)
        {
            var index = candidateIndex++;
            var nameIndex = index % DarkwoodNames.Length;
            var generation = index / DarkwoodNames.Length;
            var name = generation == 0
                ? DarkwoodNames[nameIndex]
                : generation == 1
                    ? $"{DarkwoodNames[nameIndex]} {DarkwoodEpithets[nameIndex]}"
                    : $"{DarkwoodNames[nameIndex]} {DarkwoodEpithets[nameIndex]} {generation}";
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
                Id = generation == 0
                    ? Guid.Parse($"74000000-0000-4000-8000-{index + 1:000000000000}")
                    : Guid.NewGuid(),
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

    private static SettlementResident CreateStonehavenResident(int index, string name, string role)
    {
        var canFight = role is "Stonehaven Guard" or "Hunter";
        var maximumHealth = role switch
        {
            "Stonehaven Guard" => 110,
            "Hunter" => 100,
            "Mason" or "Carpenter" or "Farmer" or "Miner" => 95,
            _ => 85
        };
        var (homeX, homeZ) = ResolveStonehavenHomePosition(index, role);
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
            PrimarySkill = PrimarySkillFor(role),
            SkillLevel = 1,
            Trait = TraitFor(index),
            Experience = 0,
            IsMajor = false,
            MemorySummary = $"{name} arrived in Stonehaven as a {role.ToLowerInvariant()} and has not yet entered the major chronicle.",
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

    private static void ConfigureStonehavenArrival(
        SettlementResident resident,
        int index,
        string role)
    {
        var maximumHealth = role switch
        {
            "Stonehaven Guard" => 110,
            "Hunter" => 100,
            "Mason" or "Carpenter" or "Farmer" or "Miner" => 95,
            _ => 85
        };
        var home = ResolveStonehavenHomePosition(index, role);
        var work = ResolveStonehavenWorkPosition(index, role);
        resident.Role = role;
        resident.MaximumHealth = maximumHealth;
        resident.Health = maximumHealth;
        resident.Status = ResidentStatus.Active;
        resident.CanFight = role is "Stonehaven Guard" or "Hunter";
        resident.PrimarySkill = PrimarySkillFor(role);
        resident.SkillLevel = 1;
        resident.Trait = TraitFor(index);
        resident.Experience = 0;
        resident.IsMajor = false;
        resident.MemorySummary =
            $"{resident.Name} returned to Stonehaven as a {role.ToLowerInvariant()} and has not yet entered the major chronicle.";
        resident.HomeX = home.X;
        resident.HomeY = 0.08f;
        resident.HomeZ = home.Z;
        resident.WorkX = work.X;
        resident.WorkY = 0.08f;
        resident.WorkZ = work.Z;
        resident.Dialogue = DialogueFor(role);
        resident.UpdatedAt = DateTimeOffset.UtcNow;
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
        "Farmer" => (-30 + index % 4 * 20, 76 + index / 4 % 2 * 25),
        "Miner" => (114 + index % 3 * 3, -108 + index % 2 * 4),
        "Carpenter" => (-20 + index % 3 * 2, -18 + index % 2 * 3),
        "Mason" => (86 + index % 3 * 5, -91 + index % 2 * 6),
        "Hunter" => (-35 + index % 5 * 17, 25 + index % 3 * 8),
        "Weaver" => (-15 + index % 4 * 2, -8),
        "Baker" => (8 + index % 3 * 2, -9),
        "Fisher" => (78 + index % 4 * 4, -20 + index % 3 * 2),
        "Tanner" => (-21 + index % 3 * 2, -26),
        "Brewer" => (13 + index % 3 * 2, -17),
        "Stablehand" => (21 + index % 3 * 2, -27),
        "Herbalist" => (-14 + index % 3 * 2, -25),
        "Scribe" => (0 + index % 3 * 2, -14),
        "Potter" => (16 + index % 3 * 2, -24),
        _ => (-10 + index % 11 * 2, -12 - index % 5 * 3)
    };

    private static (float X, float Z) ResolveStonehavenHomePosition(int index, string role)
    {
        if (role == "Farmer")
        {
            var farmhouseX = index % 2 == 0 ? -29.0f : 29.0f;
            return (farmhouseX + (index % 3 - 1) * 1.2f, 128.0f + (index / 2 % 2) * 1.2f);
        }

        var column = index % 15;
        var row = index / 15;
        return (-25.0f + column * 3.55f, -31.0f + row * 5.4f);
    }

    private static string DialogueFor(string role) => role switch
    {
        "Stonehaven Guard" => "My watch is marked on the roster. I know the wall section and the neighbors entrusted to me.",
        "Farmer" => "Stonehaven eats because these fields are worked in every season, not because food appears in a storehouse.",
        "Miner" => "Irondeep is hard country, but every sound vein strengthens Stonehaven's tools, gates, and guard.",
        "Carpenter" => "A straight beam and a sound joint will outlast any hurried patchwork.",
        "Mason" => "Every fitted stone carries part of Stonehaven's defense.",
        "Hunter" => "I read tracks beyond the wall and bring warning home before trouble reaches the gate.",
        "Weaver" => "Cloth, cord, bandages, and winter wool all begin at my loom.",
        "Baker" => "The ovens start before sunrise so every worker can carry bread into the day.",
        "Fisher" => "Mirrorwater provides, but only for those who understand its depth, weather, and changing shore.",
        "Tanner" => "Nothing from a hunt should be wasted—not hide, sinew, or bone.",
        "Brewer" => "Clean water and a careful barrel keep spirits up and sickness down.",
        "Stablehand" => "A settlement moves at the pace of the animals it cares for.",
        "Herbalist" => "The valley grows medicine beside poison. Skill is knowing which is which.",
        "Scribe" => "Names, stores, debts, births, and losses belong in the record.",
        "Potter" => "A good vessel keeps grain dry, water clean, and medicine safe.",
        _ => "Stonehaven is our home, and every pair of hands has work to do."
    };

    private static string PrimarySkillFor(string role) => role switch
    {
        "Stonehaven Guard" => "Swordsmanship",
        "Farmer" => "Farming",
        "Miner" => "Mining",
        "Carpenter" => "Carpentry",
        "Mason" => "Masonry",
        "Hunter" => "Tracking",
        "Weaver" => "Weaving",
        "Baker" => "Baking",
        "Fisher" => "Fishing",
        "Tanner" => "Leatherworking",
        "Brewer" => "Brewing",
        "Stablehand" => "Animal Handling",
        "Herbalist" => "Herbalism",
        "Scribe" => "Recordkeeping",
        "Potter" => "Pottery",
        _ => "Local Knowledge"
    };

    private static string TraitFor(int index)
    {
        string[] traits =
        [
            "Diligent", "Cautious", "Generous", "Ambitious",
            "Loyal", "Inventive", "Patient", "Sociable"
        ];
        return traits[Math.Abs(index) % traits.Length];
    }
}
