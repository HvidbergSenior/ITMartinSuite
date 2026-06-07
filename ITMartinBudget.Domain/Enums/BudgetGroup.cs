namespace ITMartinBudget.Domain.Enums;

public enum BudgetGroup
{
    Unknown = 0,

    // Income

    FixedIncome = 1,
    IncomeFromKommuneAndStat = 2,

    // Transfers (excluded from dashboard totals)

    ExternalTransfer = 5,
    OverførslerTilFraOpsparingsKonto = 6,

    // Special

    Refund = 7,
    GiftIncome = 8,
    GiftExpense = 9,

    // Food

    EverydayGrocery = 10,
    RestaurantCafe = 11,

    // Transport

    Fuel = 12,
    Parking = 13,
    OffentligTransport = 14,
    CarRepair = 15,
    CarMaintenance = 30,

    // Home

    HomeRepair = 16,
    RealkreditBolig = 35,

    // Shopping / Lifestyle

    GeneralShopping = 17,
    PersonalCare = 19,

    // Entertainment

    Entertainment = 20,
    Traveling = 27,

    // Family

    PaymentChildren = 28,
    BentOgSonjaInd = 31,
    BentOgSonjaUd = 32,

    // Financial

    Tax = 22,
    InterestsAndStock = 23,
    VibzSavings = 29,
    Forsikring = 33,
    FagforeningAKasse = 36,

    // Work

    WorkExpense = 24,

    // Misc

    Subscriptions = 25,
    Uncategorized = 26,
    FromChildren = 40
}