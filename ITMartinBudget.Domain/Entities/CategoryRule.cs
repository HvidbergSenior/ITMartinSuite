namespace ITMartinBudget.Domain.Entities;

public class CategoryRule
{
    public Guid Id { get; set; }

    public string Keyword { get; set; } = null!;

    public SubCategory SubCategory { get; set; }

    public int Priority { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}