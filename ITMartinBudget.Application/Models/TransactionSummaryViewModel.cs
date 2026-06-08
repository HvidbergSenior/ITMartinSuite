namespace ITMartinBudget.Application.Models;

public sealed class TransactionSummaryViewModel
{
    public DateTime Date { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }
}