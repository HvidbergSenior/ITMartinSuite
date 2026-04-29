namespace ITMartinBudget.Domain.Entities;

public class UnknownTransaction
{
    public Guid Id { get; set; }

    public string Description { get; set; } = null!;
}