using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Rules;

public static class TransactionRules
{
    public static readonly List<TransactionRule> Rules =
    [
        // =====================================
        // Income
        // =====================================

        ..FixedIncomeRules.Items,
        ..VariableIncomeRules.Items,

        // =====================================
        // Exact transfer overrides
        // MUST come BEFORE generic transfer
        // and MobilePay rules
        // =====================================

        ..TemporaryExactTransferRules.Items,

        // =====================================
        // Housing / Fixed Expenses
        // =====================================

        ..HousingRules.Items,
        ..InsuranceRules.Items,
        ..UnionRules.Items,
        ..TvInternetRules.Items,
        ..TaxRules.Items,

        // =====================================
        // Transfers / Refunds
        // IMPORTANT:
        // Keep AFTER income rules
        // to avoid false positives.
        // =====================================

        ..TransferRules.Items,
        ..FamilyTransferRules.Items,
        ..FromOutsideTransferRules.Items,
        ..RefundsRules.Items,

        // =====================================
        // Food & Drinks
        // =====================================

        ..GroceryRules.Items,
        ..TakeAwayRules.Items,
        ..RestaurantRules.Items,
        ..CafeRules.Items,

        // =====================================
        // MobilePay
        // =====================================

        ..MobilePayRules.Items,

        // =====================================
        // Transport
        // =====================================

        ..FuelRules.Items,
        ..ParkingRules.Items,
        ..PublicTransportRules.Items,
        ..CarRules.Items,
        ..ReparationsRules.Items,

        // =====================================
        // Shopping / Lifestyle
        // =====================================

        ..ClothingRules.Items,
        ..ElectronicsRules.Items,
        ..HomeRules.Items,
        ..BeautyRules.Items,

        // =====================================
        // Entertainment / Leisure
        // =====================================

        ..GamingRules.Items,
        ..ConcertBioRules.Items,
        ..SportsRules.Items,
        ..NorthsideRules.Items,
        ..HobbyRules.Items,
        ..LeisureRules.Items,
        ..SubscriptionRules.Items,

        // =====================================
        // Family / Kids / Pets
        // =====================================

        ..ChildrenRules.Items,
        ..PetsRules.Items,
        ..GiftRules.Items,

        // =====================================
        // Health
        // =====================================

        ..HealthRules.Items,

        // =====================================
        // Unknown fallback
        // MUST stay last
        // =====================================

        ..UnknownRules.Items,
    ];
}