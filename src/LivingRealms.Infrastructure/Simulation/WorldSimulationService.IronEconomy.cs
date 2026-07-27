using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed partial class WorldSimulationService
{
    private const float IrondeepX = 121f;
    private const float IrondeepY = 0.08f;
    private const float IrondeepZ = -103f;
    private const float IronHaulSpeedPerWorldHour = 42f;
    private const int IronPerLoad = 6;
    private const int MaximumEquipmentTier = 3;
    private const int MineGuardDailyWage = 5;
    private const int MaximumMineGuards = 2;

    private async Task AdvanceIronEconomyAsync(
        int worldHours,
        DateTimeOffset processedAt,
        long simulatedHoursBefore,
        Faction faction,
        Settlement settlement,
        IReadOnlyCollection<Creature> creatures,
        Dictionary<ResourceKind, FactionResource> resources,
        CancellationToken cancellationToken)
    {
        var ore = await database.WorldResourceNodes
            .SingleAsync(x => x.Id == LivingRealmsDbContext.IrondeepOreNodeId, cancellationToken);
        var mine = await database.WorldStructures
            .SingleAsync(x => x.Id == LivingRealmsDbContext.IrondeepMineStructureId, cancellationToken);
        var operations = await database.IronMiningOperations
            .OrderBy(x => x.Owner)
            .ToListAsync(cancellationToken);
        EnsureIronOperations(operations, processedAt);

        var stonehavenOperation = operations.Single(x => x.Owner == ResourceOwner.Stonehaven);
        var darkwoodOperation = operations.Single(x => x.Owner == ResourceOwner.Darkwood);
        var dain = await database.SettlementResidents
            .SingleAsync(x => x.Id == LivingRealmsDbContext.DainResidentId, cancellationToken);
        ConfigureDainForIrondeep(dain, stonehavenOperation);
        AssignDarkwoodMiner(darkwoodOperation, creatures);

        var stonehavenDelivered = 0;
        var darkwoodDelivered = 0;
        var darkwoodContestedIrondeep = false;
        var leader = creatures.Single(x => x.Id == faction.LeaderCreatureId);
        for (var hour = 0; hour < worldHours; hour++)
        {
            if (ore.Remaining <= 0 && ore.RespawnAt is not null && ore.RespawnAt <= processedAt)
            {
                ore.Remaining = ore.Capacity;
                ore.RespawnAt = null;
            }

            if (mine.Health > 0)
            {
                stonehavenDelivered += AdvanceIronOperation(
                    stonehavenOperation,
                    ore,
                    StonehavenIronDepot,
                    processedAt);
                darkwoodDelivered += AdvanceIronOperation(
                    darkwoodOperation,
                    ore,
                    DarkwoodIronDepot,
                    processedAt);
                darkwoodContestedIrondeep |=
                    Distance(darkwoodOperation.PositionX, darkwoodOperation.PositionZ, IrondeepX, IrondeepZ) <= 48f ||
                    darkwoodOperation.Status is IronMiningStatus.Mining or IronMiningStatus.WaitingForOre;
            }

            SynchronizeIronWorker(stonehavenOperation, dain, null, processedAt);
            SynchronizeIronWorker(
                darkwoodOperation,
                null,
                creatures.FirstOrDefault(x => x.Id == darkwoodOperation.CreatureId),
                processedAt);
        }

        if (stonehavenDelivered > 0)
        {
            settlement.Iron = Math.Min(350, settlement.Iron + stonehavenDelivered);
        }
        if (darkwoodDelivered > 0)
        {
            AddResource(resources[ResourceKind.Iron], darkwoodDelivered);
        }

        var equipmentChanges = UpgradeEquipment(settlement, faction, resources, creatures);
        var guardChanges = UpdateMineGuards(
            settlement,
            darkwoodContestedIrondeep,
            simulatedHoursBefore,
            faction.SimulatedHours,
            processedAt);

        ore.UpdatedAt = processedAt;
        foreach (var operation in operations)
        {
            operation.UpdatedAt = processedAt;
        }

        if (stonehavenDelivered > 0 || darkwoodDelivered > 0)
        {
            AddHistory(
                "iron_delivered",
                "Irondeep ore reached the faction stores",
                $"Dain delivered {stonehavenDelivered} iron to Stonehaven and {darkwoodOperation.MinerName} delivered {darkwoodDelivered} iron to Darkwood. " +
                $"The A3 vein now holds {ore.Remaining}/{ore.Capacity}; no other place in the valley creates iron.",
                2,
                faction,
                leader,
                processedAt);
        }

        if (equipmentChanges.Count > 0)
        {
            AddHistory(
                "iron_equipment_upgraded",
                "Irondeep ore became lasting arms and armor",
                string.Join(" ", equipmentChanges),
                2,
                faction,
                leader,
                processedAt);
        }

        if (guardChanges is not null)
        {
            AddHistory(
                "irondeep_guard_contract",
                "Stonehaven changed the Irondeep guard detail",
                guardChanges,
                2,
                faction,
                leader,
                processedAt);
        }
    }

    private void EnsureIronOperations(
        List<IronMiningOperation> operations,
        DateTimeOffset processedAt)
    {
        if (operations.All(x => x.Owner != ResourceOwner.Stonehaven))
        {
            var operation = CreateIronOperation(
                LivingRealmsDbContext.StonehavenIronOperationId,
                ResourceOwner.Stonehaven,
                "Dain",
                LivingRealmsDbContext.DainResidentId,
                null,
                StonehavenIronDepot,
                processedAt);
            database.IronMiningOperations.Add(operation);
            operations.Add(operation);
        }
        if (operations.All(x => x.Owner != ResourceOwner.Darkwood))
        {
            var operation = CreateIronOperation(
                LivingRealmsDbContext.DarkwoodIronOperationId,
                ResourceOwner.Darkwood,
                "Darkwood miner not yet assigned",
                null,
                null,
                DarkwoodIronDepot,
                processedAt);
            database.IronMiningOperations.Add(operation);
            operations.Add(operation);
        }
    }

    private static IronMiningOperation CreateIronOperation(
        Guid id,
        ResourceOwner owner,
        string minerName,
        Guid? residentId,
        Guid? creatureId,
        IronPoint start,
        DateTimeOffset processedAt) =>
        new()
        {
            Id = id,
            Owner = owner,
            MinerName = minerName,
            ResidentId = residentId,
            CreatureId = creatureId,
            Status = IronMiningStatus.TravelingToMine,
            PositionX = start.X,
            PositionY = IrondeepY,
            PositionZ = start.Z,
            LastTransitionAt = processedAt,
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        };

    private static void ConfigureDainForIrondeep(
        SettlementResident dain,
        IronMiningOperation operation)
    {
        dain.Role = "Iron Miner";
        dain.PrimarySkill = "Iron Mining";
        dain.Dialogue = "Irondeep is the valley's only iron vein. Ore counts only after I carry it home.";
        dain.MemorySummary =
            "Dain works the only known iron vein in A3 and records every load delivered to Stonehaven.";
        operation.ResidentId = dain.Id;
        operation.MinerName = dain.Name;
    }

    private static void AssignDarkwoodMiner(
        IronMiningOperation operation,
        IReadOnlyCollection<Creature> creatures)
    {
        var assigned = creatures.FirstOrDefault(x =>
            x.Id == operation.CreatureId &&
            x.Status == CreatureStatus.Alive &&
            x.Health > 0);
        assigned ??= creatures
            .Where(x =>
                x.FactionId == LivingRealmsDbContext.DarkwoodClanId &&
                x.Id != LivingRealmsDbContext.GoblinChiefCreatureId &&
                x.Status == CreatureStatus.Alive &&
                x.Health > 0 &&
                x.Role != "Raid Attacker")
            .OrderByDescending(x => x.Role == "Iron Miner")
            .ThenByDescending(x => x.Role == "Stone Gatherer")
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (assigned is null)
        {
            return;
        }

        assigned.Role = "Iron Miner";
        assigned.Title = "Iron Miner";
        operation.CreatureId = assigned.Id;
        operation.MinerName = assigned.Name;
    }

    private static int AdvanceIronOperation(
        IronMiningOperation operation,
        WorldResourceNode ore,
        IronPoint depot,
        DateTimeOffset processedAt)
    {
        switch (operation.Status)
        {
            case IronMiningStatus.TravelingToMine:
                if (MoveTowards(operation, new IronPoint(IrondeepX, IrondeepZ)))
                {
                    operation.Status = IronMiningStatus.Mining;
                    operation.LastTransitionAt = processedAt;
                }
                break;
            case IronMiningStatus.Mining:
                if (ore.Remaining <= 0)
                {
                    operation.Status = IronMiningStatus.WaitingForOre;
                    ore.RespawnAt ??= processedAt.AddSeconds(ore.RespawnSeconds);
                    operation.LastTransitionAt = processedAt;
                    break;
                }
                var gathered = Math.Min(IronPerLoad, ore.Remaining);
                ore.Remaining -= gathered;
                operation.CargoIron += gathered;
                operation.Status = IronMiningStatus.ReturningHome;
                operation.LastTransitionAt = processedAt;
                if (ore.Remaining == 0)
                {
                    ore.RespawnAt = processedAt.AddSeconds(ore.RespawnSeconds);
                }
                break;
            case IronMiningStatus.ReturningHome:
                if (!MoveTowards(operation, depot))
                {
                    break;
                }
                var delivered = operation.CargoIron;
                operation.CargoIron = 0;
                operation.TotalIronDelivered += delivered;
                operation.TripsCompleted++;
                operation.Status = IronMiningStatus.TravelingToMine;
                operation.LastTransitionAt = processedAt;
                return delivered;
            case IronMiningStatus.WaitingForOre:
                if (ore.Remaining > 0)
                {
                    operation.Status = IronMiningStatus.Mining;
                    operation.LastTransitionAt = processedAt;
                }
                break;
        }

        return 0;
    }

    private static bool MoveTowards(IronMiningOperation operation, IronPoint destination)
    {
        var deltaX = destination.X - operation.PositionX;
        var deltaZ = destination.Z - operation.PositionZ;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        if (distance <= IronHaulSpeedPerWorldHour || distance < 0.01f)
        {
            operation.PositionX = destination.X;
            operation.PositionY = IrondeepY;
            operation.PositionZ = destination.Z;
            return true;
        }

        var scale = IronHaulSpeedPerWorldHour / distance;
        operation.PositionX += deltaX * scale;
        operation.PositionY = IrondeepY;
        operation.PositionZ += deltaZ * scale;
        return false;
    }

    private static void SynchronizeIronWorker(
        IronMiningOperation operation,
        SettlementResident? resident,
        Creature? creature,
        DateTimeOffset processedAt)
    {
        if (resident is not null)
        {
            resident.WorkX = operation.PositionX;
            resident.WorkY = operation.PositionY;
            resident.WorkZ = operation.PositionZ;
            resident.UpdatedAt = processedAt;
        }
        if (creature is not null)
        {
            creature.PositionX = operation.PositionX;
            creature.PositionY = operation.PositionY;
            creature.PositionZ = operation.PositionZ;
            creature.LastProcessedAt = processedAt;
            creature.UpdatedAt = processedAt;
        }
    }

    private static List<string> UpgradeEquipment(
        Settlement settlement,
        Faction faction,
        Dictionary<ResourceKind, FactionResource> resources,
        IReadOnlyCollection<Creature> creatures)
    {
        var changes = new List<string>();
        var previousDarkwoodWeaponTier = faction.WeaponTier;
        var previousDarkwoodArmorTier = faction.ArmorTier;
        UpgradeEquipmentFor(
            "Stonehaven",
            () => settlement.Iron,
            value => settlement.Iron = value,
            () => settlement.WeaponTier,
            value => settlement.WeaponTier = value,
            () => settlement.ArmorTier,
            value => settlement.ArmorTier = value,
            changes);
        UpgradeEquipmentFor(
            "Darkwood",
            () => checked((int)resources[ResourceKind.Iron].Amount),
            value => resources[ResourceKind.Iron].Amount = value,
            () => faction.WeaponTier,
            value => faction.WeaponTier = value,
            () => faction.ArmorTier,
            value => faction.ArmorTier = value,
            changes);
        var weaponIncrease = faction.WeaponTier - previousDarkwoodWeaponTier;
        var armorIncrease = faction.ArmorTier - previousDarkwoodArmorTier;
        if (weaponIncrease > 0 || armorIncrease > 0)
        {
            foreach (var creature in creatures.Where(x =>
                         x.FactionId == faction.Id &&
                         x.Status == CreatureStatus.Alive &&
                         x.Health > 0))
            {
                creature.Attack += weaponIncrease * 2;
                creature.Defense += armorIncrease * 2;
            }
        }
        return changes;
    }

    private static void UpgradeEquipmentFor(
        string owner,
        Func<int> getIron,
        Action<int> setIron,
        Func<int> getWeaponTier,
        Action<int> setWeaponTier,
        Func<int> getArmorTier,
        Action<int> setArmorTier,
        List<string> changes)
    {
        while (true)
        {
            var weaponTier = getWeaponTier();
            var armorTier = getArmorTier();
            var upgradeArmor = armorTier <= weaponTier && armorTier < MaximumEquipmentTier;
            var upgradeWeapon = !upgradeArmor && weaponTier < MaximumEquipmentTier;
            if (!upgradeArmor && !upgradeWeapon)
            {
                return;
            }

            var nextTier = (upgradeArmor ? armorTier : weaponTier) + 1;
            var cost = nextTier * (upgradeArmor ? 10 : 12);
            if (getIron() < cost)
            {
                return;
            }

            setIron(getIron() - cost);
            if (upgradeArmor)
            {
                setArmorTier(nextTier);
                changes.Add($"{owner} spent {cost} iron on persistent armor tier {nextTier}.");
            }
            else
            {
                setWeaponTier(nextTier);
                changes.Add($"{owner} spent {cost} iron on persistent weapon tier {nextTier}.");
            }
        }
    }

    private string? UpdateMineGuards(
        Settlement settlement,
        bool darkwoodContestedIrondeep,
        long simulatedHoursBefore,
        long simulatedHoursAfter,
        DateTimeOffset processedAt)
    {
        var changes = new List<string>();
        if (darkwoodContestedIrondeep &&
            settlement.MineGuardCount < MaximumMineGuards &&
            settlement.TreasuryGold >= MineGuardDailyWage * MaximumMineGuards)
        {
            for (var index = settlement.MineGuardCount; index < MaximumMineGuards; index++)
            {
                var guard = CreateMineGuard(index, processedAt);
                database.SettlementResidents.Add(guard);
                settlement.Population++;
            }
            settlement.MineGuardCount = MaximumMineGuards;
            changes.Add(
                $"Stonehaven hired {MaximumMineGuards} named A3 mine guards after Darkwood approached Irondeep.");
        }

        var previousDay = (int)(simulatedHoursBefore / 24);
        var currentDay = (int)(simulatedHoursAfter / 24);
        var payableDays = Math.Max(0, currentDay - Math.Max(previousDay, settlement.LastMineGuardWageDay));
        if (payableDays > 0 && settlement.MineGuardCount > 0)
        {
            var wageDue = payableDays * settlement.MineGuardCount * MineGuardDailyWage;
            if (settlement.TreasuryGold >= wageDue)
            {
                settlement.TreasuryGold -= wageDue;
                changes.Add(
                    $"Stonehaven paid {wageDue} gold for {payableDays} world day(s) of Irondeep guard wages.");
            }
            else
            {
                var guards = database.SettlementResidents.Local
                    .Where(x => x.SettlementId == settlement.Id && x.Role == "A3 Mine Guard")
                    .Concat(database.SettlementResidents
                        .Where(x => x.SettlementId == settlement.Id && x.Role == "A3 Mine Guard"))
                    .DistinctBy(x => x.Id)
                    .ToArray();
                database.SettlementResidents.RemoveRange(guards);
                settlement.Population = Math.Max(
                    WorldPopulationService.StartingStonehavenPopulation,
                    settlement.Population - settlement.MineGuardCount);
                changes.Add(
                    $"Stonehaven could not pay {wageDue} gold; its {settlement.MineGuardCount} Irondeep guard contracts ended.");
                settlement.MineGuardCount = 0;
            }
        }
        settlement.LastMineGuardWageDay = Math.Max(settlement.LastMineGuardWageDay, currentDay);
        return changes.Count == 0 ? null : string.Join(" ", changes);
    }

    private static SettlementResident CreateMineGuard(int index, DateTimeOffset processedAt)
    {
        var names = new[] { "Roderic Ames", "Sela Brand" };
        var positions = new[] { new IronPoint(116, -99), new IronPoint(126, -99) };
        var position = positions[index];
        return new SettlementResident
        {
            Id = Guid.Parse($"77000000-0000-4000-8000-{index + 1:000000000000}"),
            SettlementId = LivingRealmsDbContext.StonehavenVillageId,
            Name = names[index],
            Role = "A3 Mine Guard",
            Health = 120,
            MaximumHealth = 120,
            Status = ResidentStatus.Active,
            CanFight = true,
            PrimarySkill = "Mine Defense",
            SkillLevel = 3,
            Trait = index == 0 ? "Watchful" : "Resolute",
            Experience = 120,
            IsMajor = false,
            MemorySummary =
                "Hired by Stonehaven at five gold per world day to protect Irondeep's workers and ore road.",
            HomeX = index == 0 ? -6 : 6,
            HomeY = IrondeepY,
            HomeZ = -22,
            WorkX = position.X,
            WorkY = IrondeepY,
            WorkZ = position.Z,
            SafeX = 0,
            SafeY = IrondeepY,
            SafeZ = -12,
            Dialogue = "My contract is five gold a day. While Stonehaven pays, Irondeep stays guarded.",
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        };
    }

    private static float Distance(float x1, float z1, float x2, float z2)
    {
        var x = x2 - x1;
        var z = z2 - z1;
        return MathF.Sqrt(x * x + z * z);
    }

    private static readonly IronPoint StonehavenIronDepot = new(8, -24);
    private static readonly IronPoint DarkwoodIronDepot = new(-116, -104);

    private readonly record struct IronPoint(float X, float Z);

    private static void ResetIronOperation(IronMiningOperation operation, DateTimeOffset resetAt)
    {
        var depot = operation.Owner == ResourceOwner.Stonehaven
            ? StonehavenIronDepot
            : DarkwoodIronDepot;
        operation.MinerName = operation.Owner == ResourceOwner.Stonehaven
            ? "Dain"
            : "Darkwood miner not yet assigned";
        operation.ResidentId = operation.Owner == ResourceOwner.Stonehaven
            ? LivingRealmsDbContext.DainResidentId
            : null;
        operation.CreatureId = null;
        operation.Status = IronMiningStatus.TravelingToMine;
        operation.PositionX = depot.X;
        operation.PositionY = IrondeepY;
        operation.PositionZ = depot.Z;
        operation.CargoIron = 0;
        operation.TotalIronDelivered = 0;
        operation.TripsCompleted = 0;
        operation.LastTransitionAt = resetAt;
        operation.UpdatedAt = resetAt;
    }
}
