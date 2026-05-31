using ITMartinBudget.Domain.Entities;

public sealed class DashboardViewModel
{
    public List<BankTransaction>
        Transactions { get; init; } = [];

    public DateTime? FirstTransactionDate { get; init; }

    public DateTime? LastTransactionDate { get; init; }

    public int MonthsLoaded { get; init; }

    public decimal TotalIncome { get; init; }

    public decimal TotalExpenses { get; init; }

    public decimal NetAmount { get; init; }

    public decimal FixedIncome { get; init; }

    public decimal FixedExpenses { get; init; }

    public decimal InternalTransferIncome { get; init; }

    public decimal InternalTransferExpenses { get; init; }

    public decimal InternalTransferNet { get; init; }

    public List<BudgetGroupSummary>
        BudgetGroupSummaries { get; init; } = [];

    public List<BankTransaction>
        UncategorizedTransactions { get; init; } = [];
}