using LivingRealms.Domain.Entities;

namespace LivingRealms.Infrastructure.Simulation;

public static class WorldSurvivalService
{
    public const int FoodConsumedPerLivingMemberPerHour = 1;
    public const int TargetFoodSurplusPerHour = 3;
    public const int MinimumFoodSurplusAfterGrowthPerHour = 2;
    public const int StonehavenFarmerFoodPerHour = 5;
    public const int StonehavenFisherFoodPerHour = 3;
    public const int StonehavenHunterFoodPerHour = 6;
    public const int DarkwoodHunterFoodPerHour = 10;

    public static FoodEconomySnapshot CalculateStonehaven(
        IEnumerable<SettlementResident> residents,
        int storedFood,
        int availableWildlife)
    {
        var living = residents
            .Where(IsLiving)
            .ToArray();
        var farmers = living.Count(x => x.Role.Equals("Farmer", StringComparison.OrdinalIgnoreCase));
        var fishers = living.Count(x => x.Role.Equals("Fisher", StringComparison.OrdinalIgnoreCase));
        var hunters = living.Count(x => x.Role.Equals("Hunter", StringComparison.OrdinalIgnoreCase));
        var farmerProduction = farmers * StonehavenFarmerFoodPerHour;
        var fisherProduction = fishers * StonehavenFisherFoodPerHour;
        var hunterProduction = availableWildlife <= 0
            ? 0
            : Math.Min(
                hunters * StonehavenHunterFoodPerHour,
                availableWildlife * 3);

        return BuildSnapshot(
            living.Length,
            storedFood,
            farmers,
            fishers,
            hunters,
            farmerProduction,
            fisherProduction,
            hunterProduction,
            ResolveStonehavenRecruitment(
                living.Length,
                farmers,
                fishers,
                hunters,
                farmerProduction + fisherProduction + hunterProduction));
    }

    public static FoodEconomySnapshot CalculateDarkwood(
        IEnumerable<Creature> creatures,
        long storedFood,
        int availableWildlife)
    {
        var living = creatures
            .Where(IsLiving)
            .ToArray();
        var hunters = living.Count(x =>
            x.Role?.Equals("Clan Hunter", StringComparison.OrdinalIgnoreCase) == true);
        var hunterProduction = availableWildlife <= 0
            ? 0
            : Math.Min(
                hunters * DarkwoodHunterFoodPerHour,
                availableWildlife * 4);

        return BuildSnapshot(
            living.Length,
            (int)Math.Min(int.MaxValue, storedFood),
            0,
            0,
            hunters,
            0,
            0,
            hunterProduction,
            ResolveDarkwoodRecruitment(living.Length, hunters, hunterProduction));
    }

    public static string ResolveStonehavenRecruitmentRole(
        IEnumerable<SettlementResident> residents,
        int availableWildlife)
    {
        var snapshot = CalculateStonehaven(residents, 0, availableWildlife);
        return snapshot.RecommendedRecruitmentRole;
    }

    public static string ResolveDarkwoodRecruitmentRole(
        IEnumerable<Creature> creatures,
        int availableWildlife)
    {
        var snapshot = CalculateDarkwood(creatures, 0, availableWildlife);
        return snapshot.RecommendedRecruitmentRole;
    }

    public static bool IsLiving(SettlementResident resident) =>
        resident.Health > 0 &&
        resident.Status is ResidentStatus.Active or ResidentStatus.Injured;

    public static bool IsLiving(Creature creature) =>
        creature.Health > 0 && creature.Status == CreatureStatus.Alive;

    public static bool IsHuntableWildlife(Creature creature) =>
        creature.FactionId is null &&
        (creature.SpeciesId == Persistence.LivingRealmsDbContext.ForestRatSpeciesId ||
         creature.SpeciesId == Persistence.LivingRealmsDbContext.PrairieWolfSpeciesId);

    private static FoodEconomySnapshot BuildSnapshot(
        int population,
        int storedFood,
        int farmers,
        int fishers,
        int hunters,
        int farmerProduction,
        int fisherProduction,
        int hunterProduction,
        string recommendedRecruitmentRole)
    {
        var produced = farmerProduction + fisherProduction + hunterProduction;
        var consumed = population * FoodConsumedPerLivingMemberPerHour;
        var net = produced - consumed;
        var hoursRemaining = consumed == 0
            ? int.MaxValue
            : storedFood / consumed;

        return new FoodEconomySnapshot(
            population,
            storedFood,
            farmers,
            fishers,
            hunters,
            farmerProduction,
            fisherProduction,
            hunterProduction,
            produced,
            consumed,
            net,
            net < 0 || storedFood == 0,
            hoursRemaining,
            recommendedRecruitmentRole);
    }

    private static string ResolveStonehavenRecruitment(
        int population,
        int farmers,
        int fishers,
        int hunters,
        int production)
    {
        if (farmers < 2)
        {
            return "Farmer";
        }
        if (fishers < 1)
        {
            return "Fisher";
        }
        if (production - population >= TargetFoodSurplusPerHour)
        {
            return "None";
        }
        if (hunters == 0)
        {
            return "Hunter";
        }
        return farmers <= fishers + hunters ? "Farmer" : "Fisher";
    }

    private static string ResolveDarkwoodRecruitment(
        int population,
        int hunters,
        int production)
    {
        if (hunters == 0 || production - population < TargetFoodSurplusPerHour)
        {
            return "Clan Hunter";
        }
        return "None";
    }
}

public sealed record FoodEconomySnapshot(
    int Population,
    int StoredFood,
    int Farmers,
    int Fishers,
    int Hunters,
    int FarmerProductionPerHour,
    int FisherProductionPerHour,
    int HunterProductionPerHour,
    int FoodProducedPerHour,
    int FoodConsumedPerHour,
    int NetFoodPerHour,
    bool IsShortage,
    int HoursOfFoodRemaining,
    string RecommendedRecruitmentRole);
