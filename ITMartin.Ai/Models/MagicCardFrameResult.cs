public sealed record MagicCardConfidence
{
    public decimal Name { get; init; }

    public decimal ManaCost { get; init; }

    public decimal CardType { get; init; }

    public decimal PowerToughness { get; init; }

    public decimal Artist { get; init; }

    public decimal CopyrightYear { get; init; }

    public decimal CollectorNumber { get; init; }

    public decimal OuterBorder { get; init; }

    public decimal FrameColor { get; init; }

    public decimal FrameStyle { get; init; }

    public decimal SetSymbolDescription { get; init; }

    public decimal RulesText { get; init; }

    public decimal FlavorText { get; init; }

    public decimal ArtworkDescription { get; init; }
}