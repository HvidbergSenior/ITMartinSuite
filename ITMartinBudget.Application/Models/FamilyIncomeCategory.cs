using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Models;

public sealed class FamilyIncomeCategory
{
    public string Name { get; set; } = "";

    public decimal MonthlyAmount { get; set; }

    public List<BankTransaction> Transactions
    {
        get;
        set;
    } = [];
}