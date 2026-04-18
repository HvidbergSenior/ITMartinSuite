namespace ITMartinBudget.Domain.Entities;

public class Transaction
{
    public DateTime Date { get; set; }
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Description { get; set; } = default!;
}