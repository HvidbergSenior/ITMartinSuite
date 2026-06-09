public sealed class MagicCardAnalysisResult
{
    public string? Name { get; set; }

    public string? Artist { get; set; }

    public string? CollectorNumber { get; set; }

    public string? CopyrightYear { get; set; }

    public string? VisibleSetSymbolDescription { get; set; }

    public bool SetSymbolVisible { get; set; }

    public bool WhiteBorder { get; set; }

    public bool OldBorder { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? PowerToughness { get; set; }

    public string? Rarity { get; set; }

    public decimal Confidence { get; set; }
}