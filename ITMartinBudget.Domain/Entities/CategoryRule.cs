using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class CategoryRule
{
    public int Id { get; set; }

    public string Pattern { get; set; } =
        string.Empty;

    public Category Category { get; set; }

    public MatchingType MatchType { get; set; } =
        MatchingType.Contains;

    public int Priority { get; set; } = 10;

    public bool IsActive { get; set; } = true;

    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;
}