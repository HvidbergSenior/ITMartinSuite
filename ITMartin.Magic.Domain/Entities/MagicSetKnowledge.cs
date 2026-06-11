public sealed class MagicSetKnowledge
{
    public string SetCode { get; set; } = null!;

    public string SetName { get; set; } = null!;

    public string SymbolDescription { get; set; } = null!;

    public string? SymbolKeywords { get; set; }

    public int ReleaseYear { get; set; }

    public bool UsesOldFrame { get; set; }

    public bool UsesWhiteBorder { get; set; }

    public bool UsesBlackBorder { get; set; }

    public bool HasCollectorNumbers { get; set; }

    public bool HasFoils { get; set; }

    public bool HasSetSymbol { get; set; }

    public string? CopyrightStyle { get; set; }
}