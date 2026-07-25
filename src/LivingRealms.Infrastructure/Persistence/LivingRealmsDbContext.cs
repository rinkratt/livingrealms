using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Persistence;

public sealed class LivingRealmsDbContext(DbContextOptions<LivingRealmsDbContext> options) : DbContext(options)
{
    public static readonly Guid StonehavenValleyId = Guid.Parse("7139a553-cea3-45e4-9d91-b3a95629b72e");
    public static readonly Guid ForestRatSpeciesId = Guid.Parse("8ac9948d-3b09-4c70-aaf1-0c36f967c5a1");
    public static readonly Guid PrairieWolfSpeciesId = Guid.Parse("5ff49fb8-b1db-4a5d-8274-8a0ee8ed4eb2");
    public static readonly Guid GoblinRaiderSpeciesId = Guid.Parse("5133411d-cb9d-4f00-a16e-ac106d7cfe91");
    public static readonly Guid GoblinChiefSpeciesId = Guid.Parse("f3260673-96f8-4d56-ad45-25901cae6f98");
    public static readonly Guid StonehavenVillageId = Guid.Parse("40000000-0000-4000-8000-000000000001");
    public static readonly Guid DarkwoodClanId = Guid.Parse("50000000-0000-4000-8000-000000000001");
    public static readonly Guid GoblinChiefCreatureId = Guid.Parse("f4c5a7b9-644f-4c85-b18f-ac38294e3001");
    public static readonly Guid StonehavenLeaderResidentId = Guid.Parse("70000000-0000-4000-8000-000000000001");
    public static readonly Guid MiraResidentId = Guid.Parse("70000000-0000-4000-8000-000000000002");
    public static readonly Guid TomasResidentId = Guid.Parse("70000000-0000-4000-8000-000000000003");
    public static readonly Guid BrannResidentId = Guid.Parse("70000000-0000-4000-8000-000000000004");
    public static readonly Guid MaraVennResidentId = Guid.Parse("70000000-0000-4000-8000-000000000005");
    public static readonly Guid ElowenResidentId = Guid.Parse("70000000-0000-4000-8000-000000000006");
    public static readonly Guid OrenResidentId = Guid.Parse("70000000-0000-4000-8000-000000000007");
    public static readonly Guid NessaResidentId = Guid.Parse("70000000-0000-4000-8000-000000000008");
    public static readonly Guid DainResidentId = Guid.Parse("70000000-0000-4000-8000-000000000009");
    public static readonly Guid StonehavenWallProjectId = Guid.Parse("81000000-0000-4000-8000-000000000001");
    public static readonly Guid DarkwoodPalisadeProjectId = Guid.Parse("81000000-0000-4000-8000-000000000002");
    public static readonly Guid StonehavenLumberYardProjectId = Guid.Parse("81000000-0000-4000-8000-000000000003");
    public static readonly Guid StonehavenQuarryWorksProjectId = Guid.Parse("81000000-0000-4000-8000-000000000004");
    public static readonly Guid DarkwoodSupplyHutProjectId = Guid.Parse("81000000-0000-4000-8000-000000000005");
    public static readonly Guid TrainingBladeItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1001");
    public static readonly Guid HuntingBowItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1002");
    public static readonly Guid LeatherGuardItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1003");
    public static readonly Guid FieldTonicItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1004");
    public static readonly Guid RatTailItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1005");
    public static readonly Guid WolfPeltItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1006");
    public static readonly Guid GoblinBladeItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1007");
    public static readonly Guid RaiderBowItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1008");
    public static readonly Guid ChiefWarbladeItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1009");
    public static readonly Guid ChiefLongbowItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1010");
    public static readonly Guid TimberItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1011");
    public static readonly Guid RoughStoneItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1012");
    public static readonly Guid RawIronItemId = Guid.Parse("105a7b69-0e17-40d0-8d0f-4aa63bfb1013");

    private static readonly DateTimeOffset SeedTime = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PhaseSixSeedTime = new(2026, 7, 17, 14, 30, 0, TimeSpan.Zero);

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterInventory> CharacterInventory => Set<CharacterInventory>();
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementResident> SettlementResidents => Set<SettlementResident>();
    public DbSet<SettlementRaid> SettlementRaids => Set<SettlementRaid>();
    public DbSet<SettlementRaidAttacker> SettlementRaidAttackers => Set<SettlementRaidAttacker>();
    public DbSet<StonehavenAssault> StonehavenAssaults => Set<StonehavenAssault>();
    public DbSet<StonehavenAssaultMember> StonehavenAssaultMembers => Set<StonehavenAssaultMember>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionResource> FactionResources => Set<FactionResource>();
    public DbSet<FactionStructure> FactionStructures => Set<FactionStructure>();
    public DbSet<CreatureSpecies> CreatureSpecies => Set<CreatureSpecies>();
    public DbSet<Creature> Creatures => Set<Creature>();
    public DbSet<CreatureSkill> CreatureSkills => Set<CreatureSkill>();
    public DbSet<CreatureEquipment> CreatureEquipment => Set<CreatureEquipment>();
    public DbSet<ScheduledEvent> ScheduledEvents => Set<ScheduledEvent>();
    public DbSet<WorldHistory> WorldHistory => Set<WorldHistory>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<WorldResourceNode> WorldResourceNodes => Set<WorldResourceNode>();
    public DbSet<ConstructionProject> ConstructionProjects => Set<ConstructionProject>();
    public DbSet<ResourceContribution> ResourceContributions => Set<ResourceContribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("living_realms");

        ConfigureAccounts(modelBuilder);
        ConfigureWorld(modelBuilder);
        ConfigureFactions(modelBuilder);
        ConfigureCreatures(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureDevelopment(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var createdAt = entityType.FindProperty(nameof(Entity.CreatedAt));
            createdAt?.SetDefaultValueSql("CURRENT_TIMESTAMP");
            var updatedAt = entityType.FindProperty(nameof(Entity.UpdatedAt));
            updatedAt?.SetDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }

    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
        });
        modelBuilder.Entity<Character>(entity =>
        {
            entity.HasIndex(x => new { x.AccountId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(40);
            entity.HasOne(x => x.Account).WithMany(x => x.Characters).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<CharacterInventory>(entity =>
        {
            entity.HasIndex(x => new { x.CharacterId, x.ItemId }).IsUnique();
            entity.HasOne(x => x.Character).WithMany(x => x.Inventory).HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CharacterSkill>(entity =>
        {
            entity.HasIndex(x => new { x.CharacterId, x.SkillKey }).IsUnique();
            entity.Property(x => x.SkillKey).HasMaxLength(80);
            entity.HasOne(x => x.Character).WithMany(x => x.Skills).HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PlayerSession>(entity =>
        {
            entity.HasIndex(x => new { x.AccountId, x.DisconnectedAt });
            entity.HasIndex(x => x.TokenHash).IsUnique().HasFilter("\"TokenHash\" IS NOT NULL");
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.Property(x => x.ConnectionId).HasMaxLength(128);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.HasOne(x => x.Account).WithMany(x => x.Sessions).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureWorld(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasData(
                CreateItem(TrainingBladeItemId, "stonehaven-training-blade", "Stonehaven Training Blade", "A balanced iron blade issued to new vanguards.", ItemKind.Weapon, ItemRarity.Common, EquipmentSlot.Weapon, CharacterArchetype.Vanguard, 5, 0, 0, 35, 6),
                CreateItem(HuntingBowItemId, "stonehaven-hunting-bow", "Stonehaven Hunting Bow", "A reliable yew bow issued to new rangers.", ItemKind.Weapon, ItemRarity.Common, EquipmentSlot.Weapon, CharacterArchetype.Ranger, 5, 0, 0, 35, 5),
                CreateItem(LeatherGuardItemId, "stonehaven-leather-guard", "Stonehaven Leather Guard", "Layered leather that softens claws and rough blades.", ItemKind.Armor, ItemRarity.Common, EquipmentSlot.Armor, null, 0, 3, 0, 30, 8),
                CreateItem(FieldTonicItemId, "field-tonic", "Field Tonic", "A sharp herbal draught that restores 35 health.", ItemKind.Consumable, ItemRarity.Uncommon, null, null, 0, 0, 35, 20, 1),
                CreateItem(RatTailItemId, "forest-rat-tail", "Forest Rat Tail", "Oren buys these as proof that the grain stores are being protected.", ItemKind.Resource, ItemRarity.Common, null, null, 0, 0, 0, 3, 1),
                CreateItem(WolfPeltItemId, "prairie-wolf-pelt", "Prairie Wolf Pelt", "A thick pelt that can be equipped as light armor.", ItemKind.Armor, ItemRarity.Uncommon, EquipmentSlot.Armor, null, 0, 5, 0, 45, 4),
                CreateItem(GoblinBladeItemId, "goblin-raider-blade", "Goblin Raider Blade", "A brutal but effective weapon recovered from a raider.", ItemKind.Weapon, ItemRarity.Uncommon, EquipmentSlot.Weapon, CharacterArchetype.Vanguard, 9, 0, 0, 80, 7),
                CreateItem(RaiderBowItemId, "goblin-raider-bow", "Goblin Raider Bow", "A horn-backed bow adapted for a Stonehaven ranger.", ItemKind.Weapon, ItemRarity.Uncommon, EquipmentSlot.Weapon, CharacterArchetype.Ranger, 8, 0, 0, 80, 6),
                CreateItem(ChiefWarbladeItemId, "gorvaks-warblade", "Gorvak's Warblade", "The heavy notched blade carried by the goblin chief.", ItemKind.Weapon, ItemRarity.Rare, EquipmentSlot.Weapon, CharacterArchetype.Vanguard, 14, 0, 0, 180, 9),
                CreateItem(ChiefLongbowItemId, "gorvaks-warbow", "Gorvak's Warbow", "A captured warbow restrung for Elara's reach.", ItemKind.Weapon, ItemRarity.Rare, EquipmentSlot.Weapon, CharacterArchetype.Ranger, 13, 0, 0, 180, 8),
                CreateItem(TimberItemId, "raw-timber", "Raw Timber", "Sound timber used by Stonehaven's builders. Construction projects and Oren both need it.", ItemKind.Resource, ItemRarity.Common, null, null, 0, 0, 0, 2, 1),
                CreateItem(RoughStoneItemId, "rough-stone", "Rough Stone", "Quarried stone used in walls and foundations. Construction projects and Oren both need it.", ItemKind.Resource, ItemRarity.Common, null, null, 0, 0, 0, 3, 2),
                CreateItem(RawIronItemId, "raw-iron-ore", "Raw Iron Ore", "Dense ore from Irondeep Mine. Brann and Oren both need dependable local iron.", ItemKind.Resource, ItemRarity.Uncommon, null, null, 0, 0, 0, 7, 3));
        });
        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasData(new Region
            {
                Id = StonehavenValleyId,
                Key = "stonehaven-valley",
                Name = "Stonehaven Valley",
                Description = "The first playable valley of Living Realms.",
                ThreatLevel = 1,
                CreatedAt = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero)
            });
        });
        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.HasIndex(x => new { x.RegionId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasOne(x => x.Region).WithMany(x => x.Settlements).HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(new Settlement
            {
                Id = StonehavenVillageId,
                RegionId = StonehavenValleyId,
                Name = "Stonehaven Village",
                Population = WorldPopulationService.StartingStonehavenPopulation,
                StructuralIntegrity = 1000,
                Food = WorldPopulationService.StartingStonehavenFood,
                Wood = WorldPopulationService.StartingStonehavenWood,
                Stone = WorldPopulationService.StartingStonehavenStone,
                Iron = WorldPopulationService.StartingStonehavenIron,
                DefenseRating = 65,
                GuardStrength = 42,
                IsDestroyed = false,
                CreatedAt = PhaseSixSeedTime,
                UpdatedAt = PhaseSixSeedTime
            });
        });
        modelBuilder.Entity<SettlementResident>(entity =>
        {
            entity.HasIndex(x => new { x.SettlementId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.Role).HasMaxLength(80);
            entity.Property(x => x.PrimarySkill).HasMaxLength(80);
            entity.Property(x => x.Trait).HasMaxLength(80);
            entity.Property(x => x.MemorySummary).HasMaxLength(500);
            entity.Property(x => x.Dialogue).HasMaxLength(500);
            entity.HasOne(x => x.Settlement)
                .WithMany(x => x.Residents)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                CreateResident(
                    StonehavenLeaderResidentId,
                    "Reeve Aldric Vale",
                    "Reeve of Stonehaven",
                    115,
                    false,
                    new(-4, 0.08f, -19),
                    new(0, 0.08f, -14),
                    new(0, 0.08f, -11),
                    "Stonehaven survives by remembering every promise, shortage, and warning before it becomes a crisis.",
                    "Administration",
                    5,
                    "Steadfast",
                    420,
                    true,
                    "The village council appointed Aldric to coordinate Stonehaven's stores, defenses, and growing households."),
                CreateResident(
                    MiraResidentId,
                    "Mira",
                    "Guard Captain",
                    135,
                    true,
                    new(-7, 0.08f, -21),
                    new(-2.5f, 0.08f, 1.5f),
                    new(-3, 0.08f, -11),
                    "The gate is quiet for now. My patrols intend to keep it that way.",
                    "Command",
                    4,
                    "Disciplined",
                    360,
                    true,
                    "Mira earned command of Stonehaven's guard after organizing the defense of the eastern farms."),
                CreateResident(
                    TomasResidentId,
                    "Tomas",
                    "Stonehaven Guard",
                    115,
                    true,
                    new(7, 0.08f, -21),
                    new(2.5f, 0.08f, 1.5f),
                    new(3, 0.08f, -11),
                    "If the horn sounds, get behind the palisade and let us hold the gate.",
                    "Patrol",
                    3,
                    "Loyal",
                    180,
                    false,
                    "Tomas has served the northern watch through three Darkwood alarms."),
                CreateResident(
                    BrannResidentId,
                    "Brann",
                    "Blacksmith",
                    105,
                    true,
                    new(-15, 0.08f, -17),
                    new(-11, 0.08f, -9.2f),
                    new(-8, 0.08f, -14),
                    "Good iron remembers the hand that shaped it. Bring me ore and I will show you.",
                    "Blacksmithing",
                    4,
                    "Exacting",
                    320,
                    true,
                    "Brann repaired the guard's weapons during Stonehaven's first recorded Darkwood raid."),
                CreateResident(
                    MaraVennResidentId,
                    "Mara Venn",
                    "Militia Recruit",
                    95,
                    true,
                    new(15, 0.08f, -18),
                    new(7, 0.08f, -2),
                    new(8, 0.08f, -14),
                    "I joined the militia because Stonehaven needed another shield, not because anyone promised I would become a hero.",
                    "Swordsmanship",
                    2,
                    "Courageous",
                    80,
                    false,
                    "Mara Venn was last seen scouting beyond the northern road; her fate remains unresolved.",
                    ResidentStatus.Missing),
                CreateResident(
                    ElowenResidentId,
                    "Elowen",
                    "Healer",
                    85,
                    false,
                    new(-16, 0.08f, -29),
                    new(-12, 0.08f, -22.6f),
                    new(-8, 0.08f, -18),
                    "Wounds heal faster when they are tended before pride makes them worse.",
                    "Medicine",
                    4,
                    "Compassionate",
                    300,
                    true,
                    "Elowen kept Stonehaven's wounded alive through the first night of the gate raid."),
                CreateResident(
                    OrenResidentId,
                    "Oren",
                    "Storekeeper",
                    95,
                    false,
                    new(16, 0.08f, -30),
                    new(12, 0.08f, -23.6f),
                    new(8, 0.08f, -18),
                    "Supplies are counted twice these days. Trouble makes every loaf and arrow matter.",
                    "Trade",
                    3,
                    "Prudent",
                    210,
                    false,
                    "Oren began recording reserve thresholds after shortages nearly stopped the wall works."),
                CreateResident(
                    NessaResidentId,
                    "Nessa",
                    "Lumberjack",
                    80,
                    false,
                    new(-7, 0.08f, -23),
                    new(-27.3f, 0.08f, -19.5f),
                    new(-4, 0.08f, -15),
                    "Every sound timber I bring home becomes a roof, a gate, or one more wall between us and Darkwood.",
                    "Woodcutting",
                    3,
                    "Resolute",
                    190,
                    false,
                    "Nessa took responsibility for the timber assigned to Stonehaven's curtain wall."),
                CreateResident(
                    DainResidentId,
                    "Dain",
                    "Quarry Worker",
                    95,
                    false,
                    new(7, 0.08f, -24),
                    new(88, 0.08f, -96),
                    new(4, 0.08f, -15),
                    "Stonehaven's walls begin in the quarry. Give me a strong back and enough daylight.",
                    "Quarrying",
                    3,
                    "Patient",
                    190,
                    false,
                    "Dain marks every stone load so the wall ledger can explain where its strength came from."));
        });
        modelBuilder.Entity<SettlementRaid>(entity =>
        {
            entity.HasIndex(x => new { x.SettlementId, x.Status, x.ScheduledAt });
            entity.HasIndex(x => new { x.AttackingFactionId, x.Status });
            entity.Property(x => x.OutcomeSummary).HasMaxLength(500);
            entity.HasOne(x => x.Settlement)
                .WithMany(x => x.Raids)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AttackingFaction)
                .WithMany()
                .HasForeignKey(x => x.AttackingFactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SettlementRaidAttacker>(entity =>
        {
            entity.HasIndex(x => x.CreatureId);
            entity.HasIndex(x => new { x.RaidId, x.CreatureId }).IsUnique();
            entity.HasIndex(x => new { x.RaidId, x.IsDefeated });
            entity.HasOne(x => x.Raid)
                .WithMany(x => x.Attackers)
                .HasForeignKey(x => x.RaidId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Creature)
                .WithMany()
                .HasForeignKey(x => x.CreatureId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefeatedByCharacter)
                .WithMany()
                .HasForeignKey(x => x.DefeatedByCharacterId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<StonehavenAssault>(entity =>
        {
            entity.HasIndex(x => new { x.DefendingFactionId, x.Status, x.StartedAt });
            entity.Property(x => x.OutcomeSummary).HasMaxLength(500);
            entity.HasOne(x => x.Settlement)
                .WithMany()
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefendingFaction)
                .WithMany()
                .HasForeignKey(x => x.DefendingFactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StonehavenAssaultMember>(entity =>
        {
            entity.HasIndex(x => new { x.AssaultId, x.ResidentId }).IsUnique();
            entity.HasIndex(x => new { x.AssaultId, x.IsDefeated });
            entity.HasOne(x => x.Assault)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.AssaultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Resident)
                .WithMany()
                .HasForeignKey(x => x.ResidentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static SettlementResident CreateResident(
        Guid id,
        string name,
        string role,
        int maximumHealth,
        bool canFight,
        Vector3Seed home,
        Vector3Seed work,
        Vector3Seed safe,
        string dialogue,
        string primarySkill,
        int skillLevel,
        string trait,
        long experience,
        bool isMajor,
        string memorySummary,
        ResidentStatus status = ResidentStatus.Active)
    {
        return new SettlementResident
        {
            Id = id,
            SettlementId = StonehavenVillageId,
            Name = name,
            Role = role,
            Health = maximumHealth,
            MaximumHealth = maximumHealth,
            Status = status,
            CanFight = canFight,
            PrimarySkill = primarySkill,
            SkillLevel = skillLevel,
            Trait = trait,
            Experience = experience,
            IsMajor = isMajor,
            MemorySummary = memorySummary,
            HomeX = home.X,
            HomeY = home.Y,
            HomeZ = home.Z,
            WorkX = work.X,
            WorkY = work.Y,
            WorkZ = work.Z,
            SafeX = safe.X,
            SafeY = safe.Y,
            SafeZ = safe.Z,
            Dialogue = dialogue,
            CreatedAt = PhaseSixSeedTime,
            UpdatedAt = PhaseSixSeedTime
        };
    }

    private readonly record struct Vector3Seed(float X, float Y, float Z);

    private static void ConfigureFactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Faction>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasData(new Faction
            {
                Id = DarkwoodClanId,
                Key = "darkwood-clan",
                Name = "Darkwood Clan",
                LeaderCreatureId = GoblinChiefCreatureId,
                Population = WorldPopulationService.StartingDarkwoodPopulation,
                TerritorySize = 1,
                Aggression = 45,
                Morale = 55,
                TechnologyLevel = 1,
                MilitaryStrength = 66,
                PopulationCapacity = 10,
                DevelopmentStage = 1,
                SimulatedHours = 0,
                LastProcessedAt = PhaseSixSeedTime,
                NextDecisionAt = PhaseSixSeedTime.AddHours(1),
                CreatedAt = PhaseSixSeedTime,
                UpdatedAt = PhaseSixSeedTime
            });
        });
        modelBuilder.Entity<FactionResource>(entity =>
        {
            entity.HasIndex(x => new { x.FactionId, x.Kind }).IsUnique();
            entity.HasOne(x => x.Faction).WithMany(x => x.Resources).HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                CreateFactionResource("51000000-0000-4000-8000-000000000001", ResourceKind.Food, 80, 250),
                CreateFactionResource("51000000-0000-4000-8000-000000000002", ResourceKind.Wood, 50, 250),
                CreateFactionResource("51000000-0000-4000-8000-000000000003", ResourceKind.Stone, 15, 180),
                CreateFactionResource("51000000-0000-4000-8000-000000000004", ResourceKind.Iron, 5, 120),
                CreateFactionResource("51000000-0000-4000-8000-000000000005", ResourceKind.Gold, 0, 100));
        });
        modelBuilder.Entity<FactionStructure>(entity =>
        {
            entity.Property(x => x.StructureType).HasMaxLength(80);
            entity.HasOne(x => x.Faction).WithMany(x => x.Structures).HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                CreateFactionStructure("52000000-0000-4000-8000-000000000001", "Hide Tents"),
                CreateFactionStructure("52000000-0000-4000-8000-000000000002", "Crude Stockpile"));
        });
    }

    private static void ConfigureCreatures(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreatureSpecies>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasData(
                CreateSpecies(
                    ForestRatSpeciesId,
                    "forest-rat",
                    "Forest Rat",
                    30,
                    4,
                    2,
                    3.2f,
                    7.0f,
                    1.35f,
                    25,
                    45),
                CreateSpecies(
                    PrairieWolfSpeciesId,
                    "prairie-wolf",
                    "Prairie Wolf",
                    55,
                    10,
                    5,
                    4.2f,
                    10.0f,
                    1.7f,
                    45,
                    75),
                CreateSpecies(
                    GoblinRaiderSpeciesId,
                    "goblin-raider",
                    "Goblin Raider",
                    90,
                    15,
                    9,
                    3.6f,
                    12.0f,
                    1.8f,
                    90,
                    120),
                CreateSpecies(
                    GoblinChiefSpeciesId,
                    "goblin-chief",
                    "Goblin Chief",
                    180,
                    22,
                    14,
                    3.2f,
                    15.0f,
                    2.1f,
                    220,
                    300));
        });
        modelBuilder.Entity<Creature>(entity =>
        {
            entity.HasIndex(x => new { x.RegionId, x.FactionId, x.Status });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Role).HasMaxLength(80);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.HasOne(x => x.Species).WithMany().HasForeignKey(x => x.SpeciesId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faction).WithMany().HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasData(
                CreateCreature(
                    "8bd3a92f-80a8-46a6-8349-427975490a01",
                    ForestRatSpeciesId,
                    "Brambletail",
                    1,
                    30,
                    4,
                    2,
                    3.2f,
                    20,
                    76,
                    0.08f,
                    68),
                CreateCreature(
                    "8bd3a92f-80a8-46a6-8349-427975490a02",
                    ForestRatSpeciesId,
                    "Mosswhisker",
                    1,
                    30,
                    4,
                    2,
                    3.2f,
                    20,
                    92,
                    0.08f,
                    78),
                CreateCreature(
                    "8bd3a92f-80a8-46a6-8349-427975490a03",
                    ForestRatSpeciesId,
                    "Thornsnout",
                    1,
                    30,
                    4,
                    2,
                    3.2f,
                    20,
                    110,
                    0.08f,
                    72),
                CreateCreature(
                    "5d8a9637-a327-4f42-8ec3-a292f548d101",
                    PrairieWolfSpeciesId,
                    "Ashfang",
                    2,
                    55,
                    10,
                    5,
                    4.2f,
                    45,
                    84,
                    0.08f,
                    101),
                CreateCreature(
                    "5d8a9637-a327-4f42-8ec3-a292f548d102",
                    PrairieWolfSpeciesId,
                    "Dusthowl",
                    2,
                    55,
                    10,
                    5,
                    4.2f,
                    45,
                    111,
                    0.08f,
                    105),
                CreateCreature(
                    "9230414d-a60d-46ca-9c59-36cc3b867201",
                    GoblinRaiderSpeciesId,
                    "Skrit",
                    5,
                    90,
                    15,
                    9,
                    3.6f,
                    70,
                    -124,
                    0.08f,
                    -99,
                    factionId: DarkwoodClanId,
                    role: "Raider"),
                CreateCreature(
                    "9230414d-a60d-46ca-9c59-36cc3b867202",
                    GoblinRaiderSpeciesId,
                    "Vrak",
                    5,
                    90,
                    15,
                    9,
                    3.6f,
                    70,
                    -107,
                    0.08f,
                    -103,
                    factionId: DarkwoodClanId,
                    role: "Raider"),
                CreateCreature(
                    "f4c5a7b9-644f-4c85-b18f-ac38294e3001",
                    GoblinChiefSpeciesId,
                    "Gorvak",
                    8,
                    180,
                    22,
                    14,
                    3.2f,
                    90,
                    -116,
                    0.08f,
                    -112,
                    "Goblin Chief",
                    DarkwoodClanId,
                    "Chief",
                    10));
        });
        modelBuilder.Entity<CreatureSkill>(entity =>
        {
            entity.HasIndex(x => new { x.CreatureId, x.SkillKey }).IsUnique();
            entity.Property(x => x.SkillKey).HasMaxLength(80);
            entity.HasOne(x => x.Creature).WithMany(x => x.Skills).HasForeignKey(x => x.CreatureId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CreatureEquipment>(entity =>
        {
            entity.HasIndex(x => new { x.CreatureId, x.Slot }).IsUnique();
            entity.Property(x => x.Slot).HasMaxLength(40);
            entity.HasOne(x => x.Creature).WithMany(x => x.Equipment).HasForeignKey(x => x.CreatureId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static CreatureSpecies CreateSpecies(
        Guid id,
        string key,
        string name,
        int health,
        int attack,
        int defense,
        float speed,
        float detectionRadius,
        float attackRange,
        int experienceReward,
        int respawnSeconds)
    {
        return new CreatureSpecies
        {
            Id = id,
            Key = key,
            Name = name,
            BaseHealth = health,
            BaseAttack = attack,
            BaseDefense = defense,
            BaseMovementSpeed = speed,
            DetectionRadius = detectionRadius,
            AttackRange = attackRange,
            ExperienceReward = experienceReward,
            RespawnSeconds = respawnSeconds,
            IsPersistentByDefault = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        };
    }

    private static Creature CreateCreature(
        string id,
        Guid speciesId,
        string name,
        int level,
        int health,
        int attack,
        int defense,
        float movementSpeed,
        int aggression,
        float x,
        float y,
        float z,
        string? title = null,
        Guid? factionId = null,
        string? role = null,
        int leadership = 0)
    {
        return new Creature
        {
            Id = Guid.Parse(id),
            SpeciesId = speciesId,
            FactionId = factionId,
            RegionId = StonehavenValleyId,
            Name = name,
            Level = level,
            Health = health,
            MaximumHealth = health,
            Attack = attack,
            Defense = defense,
            MovementSpeed = movementSpeed,
            Aggression = aggression,
            Leadership = leadership,
            PositionX = x,
            PositionY = y,
            PositionZ = z,
            SpawnX = x,
            SpawnY = y,
            SpawnZ = z,
            Role = role ?? (speciesId == GoblinChiefSpeciesId ? "Chief" : "Wild Creature"),
            Title = title,
            Status = CreatureStatus.Alive,
            LastProcessedAt = SeedTime,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        };
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledEvent>(entity =>
        {
            entity.HasIndex(x => new { x.Status, x.ScheduledAt });
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(160);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<WorldHistory>(entity =>
        {
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.RegionId, x.OccurredAt });
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.HasData(new WorldHistory
            {
                Id = Guid.Parse("60000000-0000-4000-8000-000000000001"),
                EventType = "faction_founded",
                Title = "The Darkwood Clan raised its first tents",
                Description = "Seven goblins gathered beneath Gorvak and established a crude encampment beyond Stonehaven's northern road.",
                RegionId = StonehavenValleyId,
                FactionId = DarkwoodClanId,
                CreatureId = GoblinChiefCreatureId,
                OccurredAt = PhaseSixSeedTime,
                ImportanceLevel = 2,
                CreatedAt = PhaseSixSeedTime,
                UpdatedAt = PhaseSixSeedTime
            });
        });
    }

    private static void ConfigureDevelopment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorldResourceNode>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => new { x.RegionId, x.Owner, x.Kind });
            entity.Property(x => x.Key).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                CreateResourceNode("82000000-0000-4000-8000-000000000001", "stonehaven-oak-west", "Westwood Oak", ResourceKind.Wood, ResourceOwner.Stonehaven, -39, -18, 60, 6, 90),
                CreateResourceNode("82000000-0000-4000-8000-000000000002", "stonehaven-pine-north", "Northroad Pine", ResourceKind.Wood, ResourceOwner.Stonehaven, -34, 28, 60, 6, 90),
                CreateResourceNode("82000000-0000-4000-8000-000000000003", "stonehaven-quarry-east", "Irondeep Quarry Face", ResourceKind.Stone, ResourceOwner.Stonehaven, 88, -96, 60, 5, 110),
                CreateResourceNode("82000000-0000-4000-8000-000000000004", "stonehaven-boulder-south", "Southroad Stone", ResourceKind.Stone, ResourceOwner.Stonehaven, 30, -43, 60, 5, 110),
                CreateResourceNode("82000000-0000-4000-8000-000000000005", "darkwood-pine", "Darkwood Pine", ResourceKind.Wood, ResourceOwner.Darkwood, -134, -91, 70, 6, 90),
                CreateResourceNode("82000000-0000-4000-8000-000000000006", "darkwood-deadfall", "Darkwood Deadfall", ResourceKind.Wood, ResourceOwner.Darkwood, -96, -112, 70, 6, 90),
                CreateResourceNode("82000000-0000-4000-8000-000000000007", "darkwood-stone", "Darkwood Stone Shelf", ResourceKind.Stone, ResourceOwner.Darkwood, -132, -126, 70, 5, 110),
                CreateResourceNode("82000000-0000-4000-8000-000000000008", "irondeep-ore-vein", "Irondeep Ore Vein", ResourceKind.Iron, ResourceOwner.Stonehaven, 121, -103, 45, 3, 150));
        });

        modelBuilder.Entity<ConstructionProject>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.HasOne(x => x.Settlement).WithMany().HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Faction).WithMany().HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new ConstructionProject
                {
                    Id = StonehavenWallProjectId,
                    Key = "stonehaven-curtain-wall",
                    Name = "Stonehaven Curtain Wall",
                    Owner = ResourceOwner.Stonehaven,
                    SettlementId = StonehavenVillageId,
                    PositionX = 0,
                    PositionY = 0.08f,
                    PositionZ = 5.2f,
                    WoodRequired = 240,
                    StoneRequired = 300,
                    WoodContributed = 0,
                    StoneContributed = 0,
                    CurrentLevel = 0,
                    MaximumLevel = 3,
                    CreatedAt = PhaseSixSeedTime,
                    UpdatedAt = PhaseSixSeedTime
                },
                new ConstructionProject
                {
                    Id = DarkwoodPalisadeProjectId,
                    Key = "darkwood-perimeter-palisade",
                    Name = "Darkwood Perimeter Palisade",
                    Owner = ResourceOwner.Darkwood,
                    FactionId = DarkwoodClanId,
                    PositionX = -116,
                    PositionY = 0.08f,
                    PositionZ = -87,
                    WoodRequired = 320,
                    StoneRequired = 80,
                    WoodContributed = 0,
                    StoneContributed = 0,
                    CurrentLevel = 0,
                    MaximumLevel = 3,
                    CreatedAt = PhaseSixSeedTime,
                    UpdatedAt = PhaseSixSeedTime
                },
                new ConstructionProject
                {
                    Id = StonehavenLumberYardProjectId,
                    Key = "stonehaven-lumber-yard",
                    Name = "Stonehaven Lumber Yard",
                    Owner = ResourceOwner.Stonehaven,
                    SettlementId = StonehavenVillageId,
                    PositionX = -22,
                    PositionY = 0.08f,
                    PositionZ = -19.5f,
                    WoodRequired = 120,
                    StoneRequired = 40,
                    CurrentLevel = 0,
                    MaximumLevel = 3,
                    CreatedAt = PhaseSixSeedTime,
                    UpdatedAt = PhaseSixSeedTime
                },
                new ConstructionProject
                {
                    Id = StonehavenQuarryWorksProjectId,
                    Key = "stonehaven-quarry-works",
                    Name = "Stonehaven Quarry Works",
                    Owner = ResourceOwner.Stonehaven,
                    SettlementId = StonehavenVillageId,
                    PositionX = 88,
                    PositionY = 0.08f,
                    PositionZ = -91,
                    WoodRequired = 70,
                    StoneRequired = 150,
                    CurrentLevel = 0,
                    MaximumLevel = 3,
                    CreatedAt = PhaseSixSeedTime,
                    UpdatedAt = PhaseSixSeedTime
                },
                new ConstructionProject
                {
                    Id = DarkwoodSupplyHutProjectId,
                    Key = "darkwood-supply-hut",
                    Name = "Darkwood Supply Hut",
                    Owner = ResourceOwner.Darkwood,
                    FactionId = DarkwoodClanId,
                    PositionX = -126,
                    PositionY = 0.08f,
                    PositionZ = -98,
                    WoodRequired = 100,
                    StoneRequired = 30,
                    CurrentLevel = 0,
                    MaximumLevel = 3,
                    CreatedAt = PhaseSixSeedTime,
                    UpdatedAt = PhaseSixSeedTime
                });
        });

        modelBuilder.Entity<ResourceContribution>(entity =>
        {
            entity.HasIndex(x => new { x.ConstructionProjectId, x.OccurredAt });
            entity.Property(x => x.ContributorName).HasMaxLength(120);
            entity.Property(x => x.Source).HasMaxLength(40);
            entity.HasOne(x => x.ConstructionProject).WithMany(x => x.Contributions)
                .HasForeignKey(x => x.ConstructionProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static WorldResourceNode CreateResourceNode(
        string id,
        string key,
        string name,
        ResourceKind kind,
        ResourceOwner owner,
        float x,
        float z,
        int capacity,
        int yield,
        int respawnSeconds)
    {
        return new WorldResourceNode
        {
            Id = Guid.Parse(id),
            RegionId = StonehavenValleyId,
            Key = key,
            Name = name,
            Kind = kind,
            Owner = owner,
            PositionX = x,
            PositionY = 0.08f,
            PositionZ = z,
            Remaining = capacity,
            Capacity = capacity,
            YieldPerHarvest = yield,
            RespawnSeconds = respawnSeconds,
            CreatedAt = PhaseSixSeedTime,
            UpdatedAt = PhaseSixSeedTime
        };
    }

    private static FactionResource CreateFactionResource(string id, ResourceKind kind, long amount, long capacity)
    {
        return new FactionResource
        {
            Id = Guid.Parse(id),
            FactionId = DarkwoodClanId,
            Kind = kind,
            Amount = amount,
            Capacity = capacity,
            CreatedAt = PhaseSixSeedTime,
            UpdatedAt = PhaseSixSeedTime
        };
    }

    private static FactionStructure CreateFactionStructure(string id, string structureType)
    {
        return new FactionStructure
        {
            Id = Guid.Parse(id),
            FactionId = DarkwoodClanId,
            StructureType = structureType,
            Level = 1,
            Health = 100,
            CompletedAt = PhaseSixSeedTime,
            CreatedAt = PhaseSixSeedTime,
            UpdatedAt = PhaseSixSeedTime
        };
    }

    private static Item CreateItem(
        Guid id,
        string key,
        string name,
        string description,
        ItemKind kind,
        ItemRarity rarity,
        EquipmentSlot? equipmentSlot,
        CharacterArchetype? requiredArchetype,
        int attackBonus,
        int defenseBonus,
        int healingAmount,
        int baseValue,
        int unitWeight)
    {
        return new Item
        {
            Id = id,
            Key = key,
            Name = name,
            Description = description,
            Kind = kind,
            Rarity = rarity,
            EquipmentSlot = equipmentSlot,
            RequiredArchetype = requiredArchetype,
            AttackBonus = attackBonus,
            DefenseBonus = defenseBonus,
            HealingAmount = healingAmount,
            BaseValue = baseValue,
            UnitWeight = unitWeight,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        };
    }
}
