using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class RulesFactory
{
    public static TransactionRule EverydayGrocery(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule ClothesAndShoes(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Toej,
            BudgetGroup = BudgetGroup.ExpensesBesidesGroceries,
            ComparingType = comparingType
        };
    }

    public static TransactionRule InterestsAndStock(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule ThingsOtherThanClothes(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.ExpensesBesidesGroceries,
            ComparingType = comparingType
        };
    }

    public static TransactionRule ConcertsBio(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.EntertainmentExpense,
            ComparingType = comparingType
        };
    }

    public static TransactionRule Pets(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.ExpensesBesidesGroceries,
            ComparingType = comparingType
        };
    }

    public static TransactionRule Northside(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.EntertainmentExpense,
            ComparingType = comparingType
        };
    }

    public static TransactionRule Parking(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
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
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule UnionAndAKasse(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule FromKommuneAndStat(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule FixedExpense(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule PersonalCare(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule Taxes(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule RestaurantCafe(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule Hobbies(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.Hobbies,
            ComparingType = comparingType
        };
    }

    public static TransactionRule FixedIncome(
        string pattern,
        string title,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.FixedIncome,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst,
            IsRecurring = true
        };
    }

    public static TransactionRule SavingsAndPension(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule InternalAccountTransfer(
        string pattern,
        string title,
        Category category =
            Category.Overfoersel,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule Refund(
        string pattern,
        string title = "Refund",
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule Fuel(
        string pattern,
        string title = "Fuel",
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule TransfersFromOutsideReceived(
        string pattern,
        string title = "Transfers From Outside",
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselFraIkkeFamilie,
            BudgetGroup = BudgetGroup.GiftIncome,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst
        };
    }

    public static TransactionRule TransfersToOutsideGiven(
        string pattern,
        string title = "Transfer To Outside",
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselTilIkkeFamilie,
            BudgetGroup = BudgetGroup.ExternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift
        };
    }

    public static TransactionRule TransfersFromFamily(
        string pattern,
        string title = "Transfer From Family",
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselFraFamilie,
            BudgetGroup = BudgetGroup.InternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Indkomst
        };
    }

    public static TransactionRule TransfersToFamily(
        string pattern,
        string title = "Transfer To Family",
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = Category.OverfoerselTilFamilie,
            BudgetGroup = BudgetGroup.InternalTransfer,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift
        };
    }

    public static TransactionRule GiftExpense(
        string pattern,
        string title = "Gift",
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule GiftIncome(
        string pattern,
        string title = "Gift Received",
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule EntertainmentExpense(
        string pattern,
        string title = "Entertainment",
        Category category = Category.Gaming,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.EntertainmentExpense,
            ComparingType = comparingType,
            TransactionType = TransactionType.Udgift
        };
    }

    public static TransactionRule CarRepair(
        string pattern,
        string title = "CarMechanic",
        Category category = Category.BilVedligehold,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule OtherRepairThanCar(
        string pattern,
        string title = "OtherRepairThanCar",
        Category category = Category.OtherRepairThanCar,
        ComparingType comparingType =
            ComparingType.Contains)
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

    public static TransactionRule ElectronicsBought(
        string pattern,
        string title,
        Category category,
        ComparingType comparingType =
            ComparingType.Contains)
    {
        return new()
        {
            Pattern = pattern,
            Title = title,
            Category = category,
            BudgetGroup = BudgetGroup.ElectronicDevices,
            ComparingType = comparingType
        };
    }
}