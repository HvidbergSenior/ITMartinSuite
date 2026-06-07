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

            // Transfers / Savings

            BudgetGroup.ExternalTransfer =>
                "Eksterne overførsler",
            
            BudgetGroup.VibzSavings =>
                "Vibz Opsparing & Pension",

            // Special

            BudgetGroup.Refund =>
                "Tilbagebetaling",

            BudgetGroup.GiftIncome =>
                "Gaver modtaget",

            BudgetGroup.GiftExpense =>
                "Gaver givet",

            // Food

            BudgetGroup.EverydayGrocery =>
                "Dagligvarer",

            BudgetGroup.RestaurantCafe =>
                "Restaurant & Café",

            // Transport

            BudgetGroup.Fuel =>
                "Brændstof",

            BudgetGroup.Parking =>
                "Parkering",

            BudgetGroup.OffentligTransport =>
                "Offentlig transport",

            BudgetGroup.CarRepair =>
                "Bilreparation",

            BudgetGroup.CarMaintenance =>
                "Bilvedligehold",

            // Home

            BudgetGroup.HomeRepair =>
                "Bolig & reparation",

            BudgetGroup.RealkreditBolig =>
                "Boliglån & Realkredit",

            // Lifestyle

            BudgetGroup.GeneralShopping =>
                "Shopping",

            BudgetGroup.PersonalCare =>
                "Personlig pleje",

            // Entertainment

            BudgetGroup.Entertainment =>
                "Underholdning",

            BudgetGroup.Traveling =>
                "Rejser",

            // Children

            BudgetGroup.PaymentChildren =>
                "Betalinger Til børn",
        
            BudgetGroup.Tax =>
                "Skat",

            BudgetGroup.InterestsAndStock =>
                "Aktier & renter",

            BudgetGroup.FagforeningAKasse =>
                "Fagforening & A-kasse",

            BudgetGroup.Forsikring =>
                "Forsikringer",

            // Family

            BudgetGroup.BentOgSonjaInd =>
                "Bent & Sonja ind",

            BudgetGroup.BentOgSonjaUd =>
                "Bent & Sonja ud",

            // Work

            BudgetGroup.WorkExpense =>
                "ITMartin",

            // Misc

            BudgetGroup.Subscriptions =>
                "Abonnementer",

            BudgetGroup.Uncategorized =>
                "Ukategoriseret",

            BudgetGroup.Unknown =>
                "Ukendt",

            BudgetGroup.FromChildren => 
                "Fra Børn",
            
            _ =>
                budgetGroup.ToString()
        };
    }
    public static bool IsAdjustable(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup switch
        {
            BudgetGroup.EverydayGrocery => true,
            BudgetGroup.RestaurantCafe => true,
            BudgetGroup.GeneralShopping => true,
            BudgetGroup.Entertainment => true,
            BudgetGroup.Traveling => true,

            _ => false
        };
    }

    public static bool IsFixedIncome(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup ==
               BudgetGroup.FixedIncome;
    }

    public static bool IsMandatoryExpense(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup switch
        {
            BudgetGroup.RealkreditBolig => true,
            BudgetGroup.Forsikring => true,
            BudgetGroup.FagforeningAKasse => true,

            _ => false
        };
    }

    public static bool IsRecurringAdjustable(
        this BudgetGroup budgetGroup)
    {
        return budgetGroup switch
        {
            BudgetGroup.Subscriptions => true,
            BudgetGroup.VibzSavings => true,

            _ => false
        };
    }
}