public sealed class MagicCardAnalysisResult
{
    public string? IdentifiedName { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? PowerToughness { get; set; }

    public string? Artist { get; set; }


    public string? CollectorNumber { get; set; }

    public string? OuterBorder { get; set; }

    public string? FrameColor { get; set; }

    public string? FrameStyle { get; set; }

    public string? SetSymbolDescription { get; set; }

    public decimal IdentificationConfidence { get; set; }
    
    public string? CopyrightText { get; set; }

    public string? CopyrightTextColor { get; set; }
}