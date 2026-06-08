using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RulesFactory
{
    // =====================================
    // FOOD
    // =====================================

    public static TransactionRule EverydayGrocery(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Dagligvarer,
            BudgetGroup = BudgetGroup.EverydayGrocery,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
        };
    }

    public static TransactionRule RestaurantCafe(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.RestaurantCafe,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
        };
    }

    // =====================================
    // SHOPPING
    // =====================================

    public static TransactionRule ClothesAndShoes(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.GeneralShopping,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule GeneralShopping(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.GeneralShopping,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
        };
    }
    public static TransactionRule PaymentForChildren(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.PaymentChildren,
            ComparingType = comparingType,
            RecurringIntervalMonths = 6
            
        };
    }

    // =====================================
    // ENTERTAINMENT
    // =====================================

    public static TransactionRule Entertainment(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.Entertainment,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }

    // =====================================
    // TRANSPORT
    // =====================================

    public static TransactionRule Fuel(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Braendstof,
            BudgetGroup = BudgetGroup.Fuel,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule Parking(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Parkering,
            BudgetGroup = BudgetGroup.Parking,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule PublicTransport(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OffentligTransport,
            BudgetGroup = BudgetGroup.OffentligTransport,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule CarRepair(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.CarRepair,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }
    public static TransactionRule HomeRepair(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.HomeRepair,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }
    // =====================================
    // HEALTH / PERSONAL
    // =====================================

    public static TransactionRule PersonalCare(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.PersonalCare,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }

    
    public static TransactionRule Forsikring(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 0)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.Forsikring,
            comparingType,
            recurringIntervalMonths:
            recurringIntervalMonths);
    }
    public static TransactionRule RealkreditSkatBolig(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 3)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.RealkreditBolig,
            comparingType,
            recurringIntervalMonths:
            recurringIntervalMonths);
    }

    public static TransactionRule UnionAndAKasse(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 0)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.FagforeningAKasse,
            comparingType,
            recurringIntervalMonths:
            recurringIntervalMonths);
    }

    public static TransactionRule Taxes(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.Tax,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }
    
    public static TransactionRule CarMaintenance(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 0)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.CarMaintenance,
            comparingType,
            TransactionType.Udgift,
            recurringIntervalMonths);
    }

    public static TransactionRule Subscription(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 1)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.Subscriptions,
            comparingType,
            recurringIntervalMonths:
            recurringIntervalMonths);
    }

    // =====================================
    // INCOME
    // =====================================

    public static TransactionRule Salary(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType,
        int recurringIntervalMonths = 1)
    {
        return CreateRule(
            pattern,
            title,
            category,
            BudgetGroup.FixedIncome,
            comparingType,
            TransactionType.Indkomst,
            recurringIntervalMonths);
    }
    public static TransactionRule Su(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Løn,
            BudgetGroup = BudgetGroup.FixedIncome,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 1
            
        };
    }

    public static TransactionRule FromKommuneAndStat(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.IncomeFromKommuneAndStat,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }
    
    public static TransactionRule RejserUdflugter(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.Traveling,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }
    
    public static TransactionRule Stocks(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.InterestsAndStock,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }
    
    public static TransactionRule ChildrenSavings(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.PaymentChildren,
            ComparingType = comparingType,
            RecurringIntervalMonths = 1
            
        };
    }
    public static TransactionRule TransfersOpsparingsKonto(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.OverførslerTilFraOpsparingsKonto,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }
    public static TransactionRule VibzSavingsPension(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.VibzSavings,
            ComparingType = comparingType,
            RecurringIntervalMonths = 1
            
        };
    }
    public static TransactionRule TransfersOutsideFromUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselTilUdenfor,
            BudgetGroup = BudgetGroup.ExternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule TransfersOutsideToUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselFraUdenfor,
            BudgetGroup = BudgetGroup.ExternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }
    public static TransactionRule BentOgSonjaInd(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.BentOgSonjaInd,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }
    public static TransactionRule BentOgSonjaUd(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.BentOgSonjaUd,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }
    public static TransactionRule TransfersChildrenFromUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselTilBørn,
            BudgetGroup = BudgetGroup.PaymentChildren,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule TransfersChildrenToUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselFraBørn,
            BudgetGroup = BudgetGroup.FromChildren,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }

    // =====================================
    // GIFTS / REFUNDS
    // =====================================

    public static TransactionRule GiftFromUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Gaver,
            BudgetGroup = BudgetGroup.GiftExpense,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule GiftToUs(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Gaver,
            BudgetGroup = BudgetGroup.GiftIncome,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }

    public static TransactionRule Refund(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Refund,
            BudgetGroup = BudgetGroup.Refund,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            RecurringIntervalMonths = 0
            
        };
    }

    // =====================================
    // FINANCIAL
    // =====================================

    public static TransactionRule InterestsAndStock(
        string pattern,
        string title,
        ComparingType comparingType)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Renter,
            BudgetGroup = BudgetGroup.InterestsAndStock,
            ComparingType = comparingType,
            RecurringIntervalMonths = 0
            
        };
    }
    
    private static TransactionRule CreateRule(
        string pattern,
        string title,
        Category category,
        BudgetGroup budgetGroup,
        ComparingType comparingType,
        TransactionType? transactionType = null,
        int recurringIntervalMonths = 0)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = budgetGroup,
            ComparingType = comparingType,
            RecurringIntervalMonths =
                recurringIntervalMonths,

            TransactionType =
                transactionType ??
                TransactionType.Udgift
        };
    }
}