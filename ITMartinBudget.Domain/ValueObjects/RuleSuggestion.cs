namespace ITMartinBudget.Domain.ValueObjects;

public class RuleSuggestion
{
    public string Keyword { get; set; } = "";
    public SubCategory SubCategory { get; set; }
    public int Count { get; set; }
}