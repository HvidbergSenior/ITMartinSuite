namespace ITMartinBudget.Domain.Enums;

public enum BudgetGroup
{
    Unknown = 0,

    // Income

    FixedIncome = 1,
    IncomeFromKommuneAndStat = 2,

    // Fixed recurring expenses

    FixedExpense = 3,

    // Transfers / Savings

    InternalTransfer = 4,
    ExternalTransfer = 5,
    Savings = 6,

    // Special cases

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

    // Home / Repair

    HomeRepair = 16,

    // Lifestyle / Shopping

    GeneralShopping = 17,
    PersonalCare = 19,

    // Entertainment / Leisure

    Entertainment = 20,

    // Financial

    Tax = 22,
    InterestsAndStock = 23,

    // Misc

    WorkExpense = 24,
    Subscriptions = 25,
    Uncategorized = 26,
    Traveling,
    PaymentChildren,
    VibzSavings,
    CarMaintenance,
    BentOgSonja,
    BentOgSonjaInd,
    BentOgSonjaUd
}