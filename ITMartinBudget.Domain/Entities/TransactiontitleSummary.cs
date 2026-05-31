namespace ITMartinBudget.Domain.Entities;

public class TransactionTitleSummary
{
    public string Title { get; set; } =
        string.Empty;

    public int Count { get; set; }

    public decimal Total { get; set; }
}