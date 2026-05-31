using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Extensions;

public static class BudgetGroupExtensions
{
    public static string ToDisplayName(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup switch
        {
            BudgetGroup.FixedIncome =>
                "Fast indkomst",

            BudgetGroup.FixedExpense =>
                "Faste udgifter",

            BudgetGroup.EverydayGrocery =>
                "Dagligvarer",

            BudgetGroup.GeneralShopping =>
                "Shopping",

            BudgetGroup.Savings =>
                "Opsparing",

            BudgetGroup.Subscriptions =>
                "Abonnementer",

            BudgetGroup.ExternalTransfer =>
                "Eksterne Overførsler",

            BudgetGroup.InternalTransfer =>
                "Interne Overførsler",

            BudgetGroup.CarRepair =>
                "Bilreparation",

            BudgetGroup.Fuel =>
                "Brændstof",

            BudgetGroup.WorkExpense =>
                "ITMartin",

            BudgetGroup.PersonalCare =>
                "Personlig Pleje",

            BudgetGroup.RestaurantCafe =>
                "Restaurant & Café",

            BudgetGroup.Entertainment =>
                "Underholdning",

            BudgetGroup.OffentligTransport =>
                "Offentlig transport",

            BudgetGroup.GiftExpense =>
                "Gaver udgift",

            BudgetGroup.Tax =>
                "Skat",

            BudgetGroup.Parking =>
                "Parkering",

            BudgetGroup.Refund =>
                "Tilbagebetaling",

            BudgetGroup.IncomeFromKommuneAndStat =>
                "Offentlige Ydelser",

            BudgetGroup.Uncategorized =>
                "Ukategoriseret",

            _ =>
                budgetGroup.ToString()
        };
    }
}