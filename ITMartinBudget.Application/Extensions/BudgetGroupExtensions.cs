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
            
            // Misc

            BudgetGroup.Subscriptions =>
                "Abonnementer",

            BudgetGroup.Uncategorized =>
                "Ukategoriseret",

            BudgetGroup.Unknown =>
                "Ukendt",

            BudgetGroup.FromChildren => 
                "Fra Børn",
            BudgetGroup.OverførslerTilFraOpsparingsKonto =>
                "Opsparingskonto",
            _ =>
                budgetGroup.ToString()
        };
    }
    public static BudgetGroupType GetBudgetGroupType(
        this BudgetGroup budgetGroup)
    {
        
        return budgetGroup switch
        {
            BudgetGroup.FixedIncome =>
                BudgetGroupType.FixedIncome,

            BudgetGroup.RealkreditBolig =>
                BudgetGroupType.MandatoryExpense,

            BudgetGroup.Forsikring =>
                BudgetGroupType.MandatoryExpense,

            BudgetGroup.FagforeningAKasse =>
                BudgetGroupType.MandatoryExpense,

            BudgetGroup.VibzSavings =>
                BudgetGroupType.MandatoryExpense,

            BudgetGroup.Subscriptions =>
                BudgetGroupType.RecurringAdjustable,

            BudgetGroup.Fuel =>
                BudgetGroupType.SemiAdjustable,

            BudgetGroup.Parking =>
                BudgetGroupType.SemiAdjustable,

            BudgetGroup.OffentligTransport =>
                BudgetGroupType.SemiAdjustable,

            BudgetGroup.EverydayGrocery =>
                BudgetGroupType.Adjustable,

            BudgetGroup.RestaurantCafe =>
                BudgetGroupType.Adjustable,

            BudgetGroup.GeneralShopping =>
                BudgetGroupType.Adjustable,

            BudgetGroup.Entertainment =>
                BudgetGroupType.Adjustable,

            BudgetGroup.Traveling =>
                BudgetGroupType.Adjustable,
            BudgetGroup.IncomeFromKommuneAndStat
                => BudgetGroupType.FixedIncome,

            BudgetGroup.CarRepair
                => BudgetGroupType.SemiAdjustable,

            BudgetGroup.CarMaintenance
                => BudgetGroupType.SemiAdjustable,

            BudgetGroup.HomeRepair
                => BudgetGroupType.SemiAdjustable,

            BudgetGroup.PersonalCare
                => BudgetGroupType.Adjustable,

            BudgetGroup.PaymentChildren
                => BudgetGroupType.SemiAdjustable,
            
            BudgetGroup.Tax
                => BudgetGroupType.MandatoryExpense,
            BudgetGroup.ExternalTransfer
                => BudgetGroupType.Ignore,

            BudgetGroup.OverførslerTilFraOpsparingsKonto
                => BudgetGroupType.Ignore,

            BudgetGroup.Refund
                => BudgetGroupType.Ignore,

            BudgetGroup.GiftIncome
                => BudgetGroupType.Ignore,

            BudgetGroup.GiftExpense
                => BudgetGroupType.Ignore,
            
            BudgetGroup.BentOgSonjaInd
                => BudgetGroupType.Ignore,

            BudgetGroup.BentOgSonjaUd
                => BudgetGroupType.Ignore,

            BudgetGroup.InterestsAndStock
                => BudgetGroupType.Ignore,

            BudgetGroup.Uncategorized
                => BudgetGroupType.Ignore,

            BudgetGroup.FromChildren
                => BudgetGroupType.Ignore,

            BudgetGroup.Unknown
                => BudgetGroupType.Ignore,
            _ =>
                BudgetGroupType.Ignore
        };
    }
}