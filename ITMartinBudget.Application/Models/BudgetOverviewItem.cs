using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public class BudgetOverviewItem
{
    public string Title { get; set; } = "";

    public Category Category { get; set; }

    public decimal Total { get; set; }

    public decimal MonthlyAverage { get; set; }


    public TransactionType TransactionType { get; set; }

    public List<BankTransaction> Transactions { get; set; } = [];
    
    public BudgetGroup BudgetGroup { get; set; }
}