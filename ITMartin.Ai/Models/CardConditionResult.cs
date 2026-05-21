namespace ITMartin.Ai.Models;

public sealed class CardConditionResult
{
    public string? Condition { get; set; }

    public decimal Confidence { get; set; }

    public string? Notes { get; set; }

    public List<string> VisibleIssues { get; set; }
        = [];

    public decimal EstimatedValueMultiplier { get; set; }

    public decimal? AdjustedEurValue { get; set; }

    public decimal? AdjustedUsdValue { get; set; }
}