namespace ITMartinBudget.Application.Models;

public sealed class CategorizationResult
{
    public int Total { get; set; }

    public int Categorized { get; set; }

    public int Uncategorized { get; set; }

    public decimal UncategorizedAmount { get; set; }

    public List<UncategorizedTransaction>
        UncategorizedTransactions { get; set; }
        = [];
}

public sealed class UncategorizedTransaction
{
    public string Description { get; set; }
        = string.Empty;

    public int Count { get; set; }

    public decimal TotalAmount { get; set; }
}