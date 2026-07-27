using LivingRealms.Domain.Entities;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LivingRealms.Infrastructure.Simulation;

public sealed partial class WorldSimulationService
{
    private const int StartingBankGold = 300;

    private async Task AdvanceFactionBanksAsync(
        int worldHours,
        DateTimeOffset processedAt,
        Faction faction,
        Settlement settlement,
        Dictionary<ResourceKind, FactionResource> resources,
        Creature leader,
        CancellationToken cancellationToken)
    {
        var banks = await database.FactionBanks
            .Include(x => x.Inventory)
            .OrderBy(x => x.Owner)
            .ToArrayAsync(cancellationToken);
        if (banks.Length != 2)
        {
            throw new InvalidOperationException("Both persistent faction banks must exist before the economy advances.");
        }

        var descriptions = new List<string>();
        descriptions.AddRange(TradeSettlementResources(
            banks.Single(x => x.Owner == ResourceOwner.Stonehaven),
            settlement,
            worldHours,
            processedAt));
        descriptions.AddRange(TradeDarkwoodResources(
            banks.Single(x => x.Owner == ResourceOwner.Darkwood),
            faction,
            resources,
            worldHours,
            processedAt));

        if (descriptions.Count == 0)
        {
            return;
        }

        AddHistory(
            "bank_trade",
            "The faction banks exchanged real stored supplies",
            string.Join(" ", descriptions),
            1,
            faction,
            leader,
            processedAt);
    }

    private IEnumerable<string> TradeSettlementResources(
        FactionBank bank,
        Settlement settlement,
        int worldHours,
        DateTimeOffset processedAt)
    {
        foreach (var kind in BankedResourceKinds)
        {
            var target = FactionBankRules.TargetReserve(
                ResourceOwner.Stonehaven,
                kind,
                settlement.Population,
                developmentStage: 1,
                settlement.WeaponTier,
                settlement.ArmorTier);
            var description = ExecuteBankTrade(
                bank,
                kind,
                target,
                worldHours,
                () => GetSettlementResource(settlement, kind),
                value => SetSettlementResource(settlement, kind, value),
                () => settlement.TreasuryGold,
                value => settlement.TreasuryGold = value,
                "Stonehaven",
                processedAt);
            if (description is not null)
            {
                yield return description;
            }
        }
    }

    private IEnumerable<string> TradeDarkwoodResources(
        FactionBank bank,
        Faction faction,
        Dictionary<ResourceKind, FactionResource> resources,
        int worldHours,
        DateTimeOffset processedAt)
    {
        foreach (var kind in BankedResourceKinds)
        {
            var target = FactionBankRules.TargetReserve(
                ResourceOwner.Darkwood,
                kind,
                faction.Population,
                faction.DevelopmentStage,
                faction.WeaponTier,
                faction.ArmorTier);
            var store = resources[kind];
            var gold = resources[ResourceKind.Gold];
            var description = ExecuteBankTrade(
                bank,
                kind,
                target,
                worldHours,
                () => checked((int)store.Amount),
                value => store.Amount = value,
                () => checked((int)gold.Amount),
                value =>
                {
                    gold.Amount = value;
                    gold.Capacity = Math.Max(gold.Capacity, value);
                },
                "Darkwood",
                processedAt);
            if (description is not null)
            {
                yield return description;
            }
        }
    }

    private string? ExecuteBankTrade(
        FactionBank bank,
        ResourceKind kind,
        int targetReserve,
        int worldHours,
        Func<int> getStored,
        Action<int> setStored,
        Func<int> getFactionGold,
        Action<int> setFactionGold,
        string factionName,
        DateTimeOffset processedAt)
    {
        var inventory = bank.Inventory.Single(x => x.Kind == kind);
        var stored = getStored();
        var factionGold = getFactionGold();
        var maximumQuantity = Math.Max(
            1,
            worldHours * (kind == ResourceKind.Iron ? 1 : 4));

        if (stored < targetReserve && inventory.Quantity > 0 && factionGold >= inventory.BankSellPrice)
        {
            var quantity = Math.Min(
                Math.Min(targetReserve - stored, inventory.Quantity),
                Math.Min(maximumQuantity, factionGold / inventory.BankSellPrice));
            if (quantity <= 0)
            {
                return null;
            }

            var totalGold = quantity * inventory.BankSellPrice;
            setStored(stored + quantity);
            setFactionGold(factionGold - totalGold);
            inventory.Quantity -= quantity;
            inventory.LastSoldAt = processedAt;
            inventory.UpdatedAt = processedAt;
            bank.GoldBalance += totalGold;
            bank.UpdatedAt = processedAt;
            var description =
                $"{factionName} bought {quantity} {kind.ToString().ToLowerInvariant()} from {bank.Name} for {totalGold} gold; the bank has {inventory.Quantity} left.";
            RecordBankTransaction(
                bank,
                inventory,
                BankTransactionType.FactionBought,
                quantity,
                inventory.BankSellPrice,
                totalGold,
                getFactionGold(),
                description,
                processedAt);
            return description;
        }

        if (stored <= targetReserve || bank.GoldBalance < inventory.BankBuyPrice)
        {
            return null;
        }

        var soldQuantity = Math.Min(
            Math.Min(stored - targetReserve, maximumQuantity),
            bank.GoldBalance / inventory.BankBuyPrice);
        if (soldQuantity <= 0)
        {
            return null;
        }

        var goldReceived = soldQuantity * inventory.BankBuyPrice;
        setStored(stored - soldQuantity);
        setFactionGold(factionGold + goldReceived);
        inventory.Quantity += soldQuantity;
        inventory.LastPurchasedAt = processedAt;
        inventory.UpdatedAt = processedAt;
        bank.GoldBalance -= goldReceived;
        bank.UpdatedAt = processedAt;
        var saleDescription =
            $"{factionName} sold {soldQuantity} {kind.ToString().ToLowerInvariant()} to {bank.Name} for {goldReceived} gold; the bank now holds {inventory.Quantity}.";
        RecordBankTransaction(
            bank,
            inventory,
            BankTransactionType.FactionSold,
            soldQuantity,
            inventory.BankBuyPrice,
            goldReceived,
            getFactionGold(),
            saleDescription,
            processedAt);
        return saleDescription;
    }

    private void RecordBankTransaction(
        FactionBank bank,
        FactionBankInventory inventory,
        BankTransactionType type,
        int quantity,
        int unitPrice,
        int totalGold,
        int factionGoldAfter,
        string description,
        DateTimeOffset processedAt)
    {
        database.FactionBankTransactions.Add(new FactionBankTransaction
        {
            BankId = bank.Id,
            Type = type,
            Kind = inventory.Kind,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalGold = totalGold,
            BankGoldAfter = bank.GoldBalance,
            FactionGoldAfter = factionGoldAfter,
            Description = description,
            OccurredAt = processedAt,
            CreatedAt = processedAt,
            UpdatedAt = processedAt
        });
    }

    private static int GetSettlementResource(Settlement settlement, ResourceKind kind) => kind switch
    {
        ResourceKind.Food => settlement.Food,
        ResourceKind.Wood => settlement.Wood,
        ResourceKind.Stone => settlement.Stone,
        ResourceKind.Iron => settlement.Iron,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The resource is not banked.")
    };

    private static void SetSettlementResource(Settlement settlement, ResourceKind kind, int value)
    {
        switch (kind)
        {
            case ResourceKind.Food:
                settlement.Food = value;
                break;
            case ResourceKind.Wood:
                settlement.Wood = value;
                break;
            case ResourceKind.Stone:
                settlement.Stone = value;
                break;
            case ResourceKind.Iron:
                settlement.Iron = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The resource is not banked.");
        }
    }

    private static readonly ResourceKind[] BankedResourceKinds =
    [
        ResourceKind.Food,
        ResourceKind.Wood,
        ResourceKind.Stone,
        ResourceKind.Iron
    ];

    private async Task ResetFactionBanksAsync(
        DateTimeOffset resetAt,
        CancellationToken cancellationToken)
    {
        var banks = await database.FactionBanks
            .Include(x => x.Inventory)
            .ToArrayAsync(cancellationToken);
        foreach (var bank in banks)
        {
            bank.GoldBalance = StartingBankGold;
            bank.UpdatedAt = resetAt;
            foreach (var inventory in bank.Inventory)
            {
                inventory.Quantity = 0;
                inventory.LastPurchasedAt = null;
                inventory.LastSoldAt = null;
                inventory.UpdatedAt = resetAt;
            }
        }

        database.FactionBankTransactions.RemoveRange(
            await database.FactionBankTransactions.ToArrayAsync(cancellationToken));
    }
}

public static class FactionBankRules
{
    public const int MaximumEquipmentTier = 3;

    public static int TargetReserve(
        ResourceOwner owner,
        ResourceKind kind,
        int population,
        int developmentStage,
        int weaponTier,
        int armorTier) => kind switch
        {
            ResourceKind.Food => Math.Max(24, population * 8),
            ResourceKind.Wood => owner == ResourceOwner.Stonehaven
                ? 80
                : 60 + Math.Max(1, developmentStage) * 30,
            ResourceKind.Stone => owner == ResourceOwner.Stonehaven
                ? 60
                : 45 + Math.Max(1, developmentStage) * 20,
            ResourceKind.Iron => NextEquipmentReserve(weaponTier, armorTier),
            _ => 0
        };

    private static int NextEquipmentReserve(int weaponTier, int armorTier)
    {
        if (armorTier <= weaponTier && armorTier < MaximumEquipmentTier)
        {
            return (armorTier + 1) * 10;
        }
        if (weaponTier < MaximumEquipmentTier)
        {
            return (weaponTier + 1) * 12;
        }
        return 20;
    }
}
