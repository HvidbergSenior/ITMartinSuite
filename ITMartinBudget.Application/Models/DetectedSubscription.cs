using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Models;

// A subscription/recurring payment inferred purely from behavior - same amount
// charged at a regular interval - rather than from a manually maintained rule.
// This is what catches MobilePay-style charges the categorization rules miss,
// since the free-text description is often inconsistent but the amount and
// timing are not.
public class DetectedSubscription
{
    public decimal Amount { get; set; }
    public string IntervalLabel { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public DateTime LastChargedDate { get; set; }
    public int DaysSinceLastCharge { get; set; }
    public string SampleDescription { get; set; } = string.Empty;
    public BudgetGroup BudgetGroup { get; set; }
}
