using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TransactionRules
{
    public static readonly List<TransactionRule> Rules =
    [
        // Income
        ..FixedIncomeRules.Items,
        ..VariableIncomeRules.Items,

        // Transfers
        ..TransferRules.Items,

        // Housing / Fixed
        ..HousingRules.Items,
        ..InsuranceRules.Items,
        ..UnionRules.Items,
        ..TvInternetRules.Items,

        // Food
        ..GroceryRules.Items,
        ..TakeAwayRules.Items,
        ..RestaurantRules.Items,
        ..CafeRules.Items,

        // Transport
        ..FuelRules.Items,
        ..ParkingRules.Items,
        ..PublicTransportRules.Items,
        ..CarRules.Items,

        // Shopping / Lifestyle
        ..ClothingRules.Items,
        ..ElectronicsRules.Items,
        ..HomeRules.Items,
        ..BeautyRules.Items,

        // Entertainment / Leisure
        ..StreamingRules.Items,
        ..GamingRules.Items,
        ..ConcertBioRules.Items,
        ..SportsRules.Items,
        ..NorthsideRules.Items,
        ..HobbyRules.Items,
        ..LeisureRules.Items,
        ..SubscriptionRules.Items,

        // Family
        ..ChildrenRules.Items,
        ..PetsRules.Items,
        ..GiftRules.Items,

        // Health
        ..HealthRules.Items,

        // Fallback
        ..UnknownRules.Items,
    ];
}