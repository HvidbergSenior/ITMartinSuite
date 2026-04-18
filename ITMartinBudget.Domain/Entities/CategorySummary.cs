namespace ITMartinBudget.Domain.Entities;

public class CategorySummary
{
    public string Category { get; set; } = default!;
    public decimal Total { get; set; }
}