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

    ExpensesBesidesGroceries = 17,
    ElectronicDevices = 18,
    PersonalCare = 19,

    // Entertainment / Leisure

    EntertainmentExpense = 20,
    Hobbies = 21,

    // Financial

    Tax = 22,
    InterestsAndStock = 23,

    // Misc

    CompanyExpense = 24,
    Uncategorized = 25
}