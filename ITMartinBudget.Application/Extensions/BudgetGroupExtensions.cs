using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Extensions;

public static class BudgetGroupExtensions
{
    public static string ToDisplayName(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup switch
        {
            // Income

            BudgetGroup.FixedIncome =>
                "Fast indkomst",

            BudgetGroup.IncomeFromKommuneAndStat =>
                "Offentlige ydelser",

            // Expenses

            BudgetGroup.FixedExpense =>
                "Faste udgifter",

            BudgetGroup.EverydayGrocery =>
                "Dagligvarer",

            BudgetGroup.GeneralShopping =>
                "Shopping",

            BudgetGroup.RestaurantCafe =>
                "Restaurant & Café",

            BudgetGroup.Subscriptions =>
                "Abonnementer",

            BudgetGroup.PersonalCare =>
                "Personlig pleje",

            BudgetGroup.Entertainment =>
                "Underholdning",

            BudgetGroup.Traveling =>
                "Rejser",

            // Children

            BudgetGroup.PaymentChildren =>
                "BetaltBørn",

            // Transport

            BudgetGroup.Fuel =>
                "Brændstof",

            BudgetGroup.Parking =>
                "Parkering",

            BudgetGroup.OffentligTransport =>
                "Offentlig transport",

            BudgetGroup.CarRepair =>
                "Bilreparation",

            // Home

            BudgetGroup.HomeRepair =>
                "Bolig & reparation",

            // Financial

            BudgetGroup.Savings =>
                "Opsparing",

            BudgetGroup.VibzSavings =>
                "Vibz Opsparing Og Pension",

            BudgetGroup.InterestsAndStock =>
                "Aktier & renter",

            BudgetGroup.Tax =>
                "Skat",

            // Transfers

            BudgetGroup.ExternalTransfer =>
                "Eksterne overførsler",

            BudgetGroup.InternalTransfer =>
                "Interne overførsler",

            // Misc

            BudgetGroup.WorkExpense =>
                "ITMartin",

            BudgetGroup.GiftIncome =>
                "Gaver modtaget",

            BudgetGroup.GiftExpense =>
                "Gaver Givet",

            BudgetGroup.Refund =>
                "Tilbagebetaling",

            BudgetGroup.Uncategorized =>
                "Ukategoriseret",

            BudgetGroup.Unknown =>
                "Ukendt",

            _ =>
                budgetGroup.ToString()
        };
    }
}