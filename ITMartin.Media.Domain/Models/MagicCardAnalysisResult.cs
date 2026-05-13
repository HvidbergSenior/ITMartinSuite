namespace ITMartin.Media.Domain.Models;

public class MagicCardAnalysisResult
{
    public string CardName { get; set; } = "";

    public string? SetCode { get; set; }

    public string? CollectorNumber { get; set; }

    public string? Artist { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? Rarity { get; set; }

    public bool IsOldBorder { get; set; }

    public bool IsWhiteBorder { get; set; }

    public bool ExactPrintingCertain { get; set; }

    public decimal Confidence { get; set; }
}