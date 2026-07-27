namespace LivingRealms.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Account : Entity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdministrator { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public ICollection<Character> Characters { get; set; } = [];
    public ICollection<PlayerSession> Sessions { get; set; } = [];
}

public sealed class Character : Entity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public required string Name { get; set; }
    public CharacterArchetype Archetype { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int Health { get; set; } = 100;
    public int MaximumHealth { get; set; } = 100;
    public int Gold { get; set; }
    public int CarryCapacity { get; set; } = 80;
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public DateTimeOffset? LastAttackAt { get; set; }
    public DateTimeOffset? LastGatherAt { get; set; }
    public DateTimeOffset? LastContributionAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? LastLogoutAt { get; set; }
    public ICollection<CharacterInventory> Inventory { get; set; } = [];
    public ICollection<CharacterSkill> Skills { get; set; } = [];
}

public sealed class Item : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ItemKind Kind { get; set; }
    public ItemRarity Rarity { get; set; }
    public EquipmentSlot? EquipmentSlot { get; set; }
    public CharacterArchetype? RequiredArchetype { get; set; }
    public int AttackBonus { get; set; }
    public int DefenseBonus { get; set; }
    public int HealingAmount { get; set; }
    public int BaseValue { get; set; }
    public int UnitWeight { get; set; } = 1;
}

public sealed class CharacterInventory : Entity
{
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public bool IsEquipped { get; set; }
}

public sealed class CharacterSkill : Entity
{
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public required string SkillKey { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public sealed class Region : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int ThreatLevel { get; set; }
    public ICollection<Settlement> Settlements { get; set; } = [];
}

public sealed class Settlement : Entity
{
    public Guid RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public required string Name { get; set; }
    public int Population { get; set; }
    public int StructuralIntegrity { get; set; } = 1000;
    public int Food { get; set; }
    public int Wood { get; set; }
    public int Stone { get; set; }
    public int Iron { get; set; }
    public int DefenseRating { get; set; }
    public int GuardStrength { get; set; }
    public int WeaponTier { get; set; }
    public int ArmorTier { get; set; }
    public int TreasuryGold { get; set; }
    public int MineGuardCount { get; set; }
    public int LastMineGuardWageDay { get; set; }
    public DateTimeOffset? LastAttackedAt { get; set; }
    public bool IsDestroyed { get; set; }
    public ICollection<SettlementResident> Residents { get; set; } = [];
    public ICollection<SettlementRaid> Raids { get; set; } = [];
}

public sealed class SettlementResident : Entity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;
    public required string Name { get; set; }
    public required string Role { get; set; }
    public int Health { get; set; } = 100;
    public int MaximumHealth { get; set; } = 100;
    public ResidentStatus Status { get; set; } = ResidentStatus.Active;
    public bool CanFight { get; set; }
    public required string PrimarySkill { get; set; }
    public int SkillLevel { get; set; } = 1;
    public required string Trait { get; set; }
    public long Experience { get; set; }
    public bool IsMajor { get; set; }
    public required string MemorySummary { get; set; }
    public float HomeX { get; set; }
    public float HomeY { get; set; }
    public float HomeZ { get; set; }
    public float WorkX { get; set; }
    public float WorkY { get; set; }
    public float WorkZ { get; set; }
    public float SafeX { get; set; }
    public float SafeY { get; set; }
    public float SafeZ { get; set; }
    public required string Dialogue { get; set; }
}

public sealed class SettlementRaid : Entity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;
    public Guid AttackingFactionId { get; set; }
    public Faction AttackingFaction { get; set; } = null!;
    public SettlementRaidStatus Status { get; set; } = SettlementRaidStatus.Scheduled;
    public DarkwoodRaidPhase Phase { get; set; } = DarkwoodRaidPhase.Assembling;
    public int PhaseRound { get; set; }
    public int WorldDay { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastAdvancedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int InitialAttackerStrength { get; set; }
    public int AttackerStrength { get; set; }
    public int InitialDefenderStrength { get; set; }
    public int DefenderStrength { get; set; }
    public int InitialStructureStrength { get; set; }
    public int StructureStrength { get; set; }
    public int PlayerContribution { get; set; }
    public int SettlementDamage { get; set; }
    public int ResidentCasualties { get; set; }
    public int ResidentInjuries { get; set; }
    public string? OutcomeSummary { get; set; }
    public ICollection<SettlementRaidAttacker> Attackers { get; set; } = [];
}

public sealed class SettlementRaidAttacker : Entity
{
    public Guid RaidId { get; set; }
    public SettlementRaid Raid { get; set; } = null!;
    public Guid CreatureId { get; set; }
    public Creature Creature { get; set; } = null!;
    public bool IsDefeated { get; set; }
    public Guid? DefeatedByCharacterId { get; set; }
    public Character? DefeatedByCharacter { get; set; }
    public DateTimeOffset? DefeatedAt { get; set; }
}

public sealed class StonehavenAssault : Entity
{
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;
    public Guid DefendingFactionId { get; set; }
    public Faction DefendingFaction { get; set; } = null!;
    public StonehavenAssaultStatus Status { get; set; } = StonehavenAssaultStatus.Assembling;
    public int PhaseRound { get; set; }
    public int WorldDay { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? LastAdvancedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int InitialSoldierCount { get; set; }
    public int SoldiersRemaining { get; set; }
    public int InitialGoblinCount { get; set; }
    public int GoblinsRemaining { get; set; }
    public int CampLevelBefore { get; set; }
    public int CampLevelAfter { get; set; }
    public int InitialCampStrength { get; set; }
    public int CampStrength { get; set; }
    public int StonehavenCasualties { get; set; }
    public int DarkwoodCasualties { get; set; }
    public string? OutcomeSummary { get; set; }
    public ICollection<StonehavenAssaultMember> Members { get; set; } = [];
}

public sealed class StonehavenAssaultMember : Entity
{
    public Guid AssaultId { get; set; }
    public StonehavenAssault Assault { get; set; } = null!;
    public Guid ResidentId { get; set; }
    public SettlementResident Resident { get; set; } = null!;
    public bool IsDefeated { get; set; }
    public DateTimeOffset? DefeatedAt { get; set; }
}

public sealed class Faction : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public Guid? LeaderCreatureId { get; set; }
    public int Population { get; set; }
    public int TerritorySize { get; set; }
    public int Aggression { get; set; }
    public int Morale { get; set; }
    public int TechnologyLevel { get; set; }
    public int MilitaryStrength { get; set; }
    public int WeaponTier { get; set; }
    public int ArmorTier { get; set; }
    public int PopulationCapacity { get; set; }
    public int DevelopmentStage { get; set; } = 1;
    public long SimulatedHours { get; set; }
    public DateTimeOffset LastProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextDecisionAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<FactionResource> Resources { get; set; } = [];
    public ICollection<FactionStructure> Structures { get; set; } = [];
}

public sealed class IronMiningOperation : Entity
{
    public ResourceOwner Owner { get; set; }
    public required string MinerName { get; set; }
    public Guid? ResidentId { get; set; }
    public SettlementResident? Resident { get; set; }
    public Guid? CreatureId { get; set; }
    public Creature? Creature { get; set; }
    public IronMiningStatus Status { get; set; } = IronMiningStatus.TravelingToMine;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int CargoIron { get; set; }
    public int TotalIronDelivered { get; set; }
    public int TripsCompleted { get; set; }
    public DateTimeOffset LastTransitionAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FactionBank : Entity
{
    public ResourceOwner Owner { get; set; }
    public required string Name { get; set; }
    public int GoldBalance { get; set; }
    public ICollection<FactionBankInventory> Inventory { get; set; } = [];
    public ICollection<FactionBankTransaction> Transactions { get; set; } = [];
}

public sealed class FactionBankInventory : Entity
{
    public Guid BankId { get; set; }
    public FactionBank Bank { get; set; } = null!;
    public ResourceKind Kind { get; set; }
    public int Quantity { get; set; }
    public int BankBuyPrice { get; set; }
    public int BankSellPrice { get; set; }
    public DateTimeOffset? LastPurchasedAt { get; set; }
    public DateTimeOffset? LastSoldAt { get; set; }
}

public sealed class FactionBankTransaction : Entity
{
    public Guid BankId { get; set; }
    public FactionBank Bank { get; set; } = null!;
    public BankTransactionType Type { get; set; }
    public ResourceKind Kind { get; set; }
    public int Quantity { get; set; }
    public int UnitPrice { get; set; }
    public int TotalGold { get; set; }
    public int BankGoldAfter { get; set; }
    public int FactionGoldAfter { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class SettlementRecovery : Entity
{
    public ResourceOwner Owner { get; set; }
    public SettlementRecoveryStatus Status { get; set; } = SettlementRecoveryStatus.Healthy;
    public int FoundingPopulation { get; set; }
    public DateTimeOffset? DefeatedAt { get; set; }
    public DateTimeOffset? RecoveryEligibleAt { get; set; }
    public DateTimeOffset? RebuildingStartedAt { get; set; }
    public DateTimeOffset? LastProgressedAt { get; set; }
    public DateTimeOffset? RecoveredAt { get; set; }
    public string? CurrentStructureKey { get; set; }
    public int RebuildCycles { get; set; }
}

public sealed class FactionResource : Entity
{
    public Guid FactionId { get; set; }
    public Faction Faction { get; set; } = null!;
    public ResourceKind Kind { get; set; }
    public long Amount { get; set; }
    public long Capacity { get; set; }
}

public sealed class FactionStructure : Entity
{
    public Guid FactionId { get; set; }
    public Faction Faction { get; set; } = null!;
    public required string StructureType { get; set; }
    public int Level { get; set; } = 1;
    public int Health { get; set; } = 100;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class WorldStructure : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public ResourceOwner Owner { get; set; }
    public WorldStructureKind Kind { get; set; }
    public Guid? ConstructionProjectId { get; set; }
    public ConstructionProject? ConstructionProject { get; set; }
    public int RequiredProjectLevel { get; set; }
    public int RequiredDevelopmentStage { get; set; } = 1;
    public int Health { get; set; }
    public int MaximumHealth { get; set; }
    public int Armor { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset? LastDamagedAt { get; set; }
    public DateTimeOffset? DestroyedAt { get; set; }
}

public sealed class WorldResourceNode : Entity
{
    public Guid RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public required string Key { get; set; }
    public required string Name { get; set; }
    public ResourceKind Kind { get; set; }
    public ResourceOwner Owner { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int Remaining { get; set; }
    public int Capacity { get; set; }
    public int YieldPerHarvest { get; set; }
    public int RespawnSeconds { get; set; }
    public DateTimeOffset? RespawnAt { get; set; }
}

public sealed class ConstructionProject : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public ResourceOwner Owner { get; set; }
    public Guid? SettlementId { get; set; }
    public Settlement? Settlement { get; set; }
    public Guid? FactionId { get; set; }
    public Faction? Faction { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int WoodRequired { get; set; }
    public int StoneRequired { get; set; }
    public int WoodContributed { get; set; }
    public int StoneContributed { get; set; }
    public int CurrentLevel { get; set; }
    public int MaximumLevel { get; set; } = 3;
    public DateTimeOffset? LastNpcContributionAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<ResourceContribution> Contributions { get; set; } = [];
}

public sealed class ResourceContribution : Entity
{
    public Guid ConstructionProjectId { get; set; }
    public ConstructionProject ConstructionProject { get; set; } = null!;
    public Guid? CharacterId { get; set; }
    public Character? Character { get; set; }
    public required string ContributorName { get; set; }
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class CreatureSpecies : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public int BaseHealth { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public float BaseMovementSpeed { get; set; }
    public float DetectionRadius { get; set; }
    public float AttackRange { get; set; }
    public int ExperienceReward { get; set; }
    public int RespawnSeconds { get; set; } = 60;
    public bool IsPersistentByDefault { get; set; }
}

public sealed class Creature : Entity
{
    public Guid SpeciesId { get; set; }
    public CreatureSpecies Species { get; set; } = null!;
    public Guid? FactionId { get; set; }
    public Faction? Faction { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public required string Name { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int Health { get; set; }
    public int MaximumHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public float MovementSpeed { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
    public float SpawnZ { get; set; }
    public int Aggression { get; set; }
    public int Leadership { get; set; }
    public string? Role { get; set; }
    public string? Title { get; set; }
    public CreatureStatus Status { get; set; } = CreatureStatus.Alive;
    public DateTimeOffset? LastAttackAt { get; set; }
    public DateTimeOffset? RespawnAt { get; set; }
    public DateTimeOffset LastProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CreatureSkill> Skills { get; set; } = [];
    public ICollection<CreatureEquipment> Equipment { get; set; } = [];
}

public sealed class CreatureSkill : Entity
{
    public Guid CreatureId { get; set; }
    public Creature Creature { get; set; } = null!;
    public required string SkillKey { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
}

public sealed class CreatureEquipment : Entity
{
    public Guid CreatureId { get; set; }
    public Creature Creature { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public required string Slot { get; set; }
}

public sealed class ScheduledEvent : Entity
{
    public required string EventType { get; set; }
    public Guid? TargetId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public ScheduledEventStatus Status { get; set; } = ScheduledEventStatus.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IdempotencyKey { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class WorldHistory : Entity
{
    public required string EventType { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FactionId { get; set; }
    public Guid? CreatureId { get; set; }
    public Guid? CharacterId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int ImportanceLevel { get; set; }
}

public sealed class PlayerSession : Entity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid? CharacterId { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? DisconnectedAt { get; set; }
    public string? TokenHash { get; set; }
    public string? ConnectionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public enum CharacterArchetype { Vanguard, Ranger }
public enum ItemKind { Miscellaneous, Weapon, Armor, Consumable, Resource, Quest }
public enum ItemRarity { Common, Uncommon, Rare, Epic }
public enum EquipmentSlot { Weapon, Armor }
public enum ResourceKind { Food, Wood, Stone, Iron, Gold }
public enum ResourceOwner { Stonehaven, Darkwood }
public enum IronMiningStatus { TravelingToMine, Mining, ReturningHome, WaitingForOre }
public enum BankTransactionType { FactionSold, FactionBought }
public enum SettlementRecoveryStatus { Healthy, Defeated, Rebuilding }
public enum WorldStructureKind { Wall, Gate, Building, Farm, Mine, Dock, Stockpile }
public enum CreatureStatus { Alive, Dead, Missing, Captured, Promoted, Retired }
public enum ResidentStatus { Active, Injured, Missing, Dead }
public enum SettlementRaidStatus { Scheduled, Active, DefendersWon, AttackersWon, Cancelled }
public enum DarkwoodRaidPhase
{
    Assembling,
    Marching,
    FightingDefenders,
    AttackingStructures,
    Resolved
}
public enum StonehavenAssaultStatus
{
    Assembling,
    Marching,
    FightingGoblins,
    AttackingCamp,
    StonehavenVictory,
    DarkwoodVictory,
    Cancelled
}
public enum ScheduledEventStatus { Pending, Processing, Completed, Failed, Cancelled }
