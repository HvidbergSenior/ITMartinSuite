using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

public class CategorizationResult
{
    public Category Category { get; set; }

    public int? RuleId { get; set; }

    public bool NeedsReview { get; set; }

    public decimal Confidence { get; set; }
}