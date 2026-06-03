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
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            ComparingType = comparingType
        };
    }
    public static TransactionRule WorkExpense(
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
            BudgetGroup = BudgetGroup.WorkExpense,
            ComparingType = comparingType
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
            TransactionType = TransactionType.Udgift
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
            TransactionType = TransactionType.Udgift
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
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            TransactionType = TransactionType.Udgift
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
            TransactionType = TransactionType.Udgift
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
            ComparingType = comparingType
        };
    }

    // =====================================
    // FIXED EXPENSES
    // =====================================

    public static TransactionRule FixedExpense(
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
            BudgetGroup = BudgetGroup.FixedExpense,
            ComparingType = comparingType,
            IsRecurring = true
        };
    }
    public static TransactionRule Forsikring(
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
            BudgetGroup = BudgetGroup.FixedExpense,
            ComparingType = comparingType,
            IsRecurring = true
        };
    }
    public static TransactionRule RealkreditSkatBolig(
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
            BudgetGroup = BudgetGroup.FixedExpense,
            ComparingType = comparingType,
            IsRecurring = true
        };
    }

    public static TransactionRule UnionAndAKasse(
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
            BudgetGroup = BudgetGroup.FixedExpense,
            ComparingType = comparingType,
            IsRecurring = true
        };
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
            IsRecurring = true
        };
    }
    public static TransactionRule CarMaintenance(
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
            BudgetGroup = BudgetGroup.CarMaintenance,
            ComparingType = comparingType,
            IsRecurring = true
        };
    }

    public static TransactionRule Subscription(
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
            BudgetGroup = BudgetGroup.Subscriptions,
            ComparingType = comparingType,
            IsRecurring = true
        };
    }

    // =====================================
    // INCOME
    // =====================================

    public static TransactionRule Salary(
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
            BudgetGroup = BudgetGroup.FixedIncome,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            IsRecurring = true
        };
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
            IsRecurring = true
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
            TransactionType = TransactionType.Indkomst
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
            TransactionType = TransactionType.Udgift
        };
    }

    // =====================================
    // TRANSFERS
    // =====================================

    public static TransactionRule InternalAccountTransfer(
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
            BudgetGroup = BudgetGroup.InternalTransfer,
            ComparingType = comparingType
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
            ComparingType = comparingType
        };
    }
    public static TransactionRule SavingsAndPension(
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
            BudgetGroup = BudgetGroup.Savings,
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            ComparingType = comparingType
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
            TransactionType = TransactionType.Udgift
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
            TransactionType = TransactionType.Indkomst
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
            TransactionType = TransactionType.Indkomst
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
            TransactionType = TransactionType.Udgift
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
            TransactionType = TransactionType.Udgift
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
            BudgetGroup = BudgetGroup.InternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst
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
            TransactionType = TransactionType.Udgift
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
            TransactionType = TransactionType.Indkomst
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
            TransactionType = TransactionType.Indkomst
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
            ComparingType = comparingType
        };
    }
}