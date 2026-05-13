namespace ITMartin.Media.Domain.Models;

public class MagicCardAnalysisResult
{
    // =====================================
    // CORE
    // =====================================

    public string CardName { get; set; } = "";

    public decimal Confidence { get; set; }

    public bool ExactPrintingCertain { get; set; }

    // =====================================
    // PRINT IDENTIFICATION
    // =====================================

    public string? SetCode { get; set; }

    public string? CollectorNumber { get; set; }

    public string? Copyright { get; set; }

    public string? ReleaseEra { get; set; }

    // =====================================
    // VISUAL FRAME ANALYSIS
    // =====================================

    public bool IsOldBorder { get; set; }

    public bool IsWhiteBorder { get; set; }

    // =====================================
    // CARD CONTENT
    // =====================================

    public string? Artist { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? Rarity { get; set; }

    public string? PowerToughness { get; set; }
}