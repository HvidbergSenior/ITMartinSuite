namespace ITMartinBudget.Domain.Entities;

public class CategoryRule
{
    public int Id { get; set; }

    public string Keyword { get; set; } = string.Empty;

    public SubCategory SubCategory { get; set; }

    public int Priority { get; set; } = 10;

    public bool IsActive { get; set; } = true;

    // 🔥 NEW
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}