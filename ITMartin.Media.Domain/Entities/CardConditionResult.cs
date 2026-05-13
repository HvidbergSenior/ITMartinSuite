namespace ITMartin.Media.Domain.Entities;

public class CardConditionResult
{
    public string Condition { get; set; } = "";

    public double Confidence { get; set; }

    public List<string> Issues { get; set; } = [];

    public string Notes { get; set; } = "";

    public decimal EstimatedValueMultiplier { get; set; }

    public decimal? AdjustedEurValue { get; set; }

    public decimal? AdjustedUsdValue { get; set; }
}