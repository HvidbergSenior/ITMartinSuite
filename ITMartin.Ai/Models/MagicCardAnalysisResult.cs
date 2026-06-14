namespace ITMartin.Ai.Models;

public sealed class MagicCardAnalysisResult
{
    public string? IdentifiedName { get; set; }

    public string? CollectorNumber { get; set; }

    public string? Artist { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? PowerToughness { get; set; }

    public decimal IdentificationConfidence { get; set; }
}