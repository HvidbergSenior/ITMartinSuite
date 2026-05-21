namespace ITMartin.Ai.Models;

public sealed class MagicCardAnalysisResult
{
    public string? Name { get; set; }

    public string? Artist { get; set; }

    public string? SetCode { get; set; }

    public string? CollectorNumber { get; set; }

    public bool OldBorder { get; set; }

    public bool WhiteBorder { get; set; }

    public string? PowerToughness { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? Rarity { get; set; }

    public decimal Confidence { get; set; }

    public bool ExactPrintingCertain { get; set; }
}