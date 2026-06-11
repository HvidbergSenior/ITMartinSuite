namespace ITMartin.Magic.Domain.Entities;

public class MagicSet
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public DateTime ReleasedAt { get; set; }

    public bool HasExpansionSymbol { get; set; }

    public string BorderColor { get; set; } = "";

    public string FrameStyle { get; set; } = "";

    public string SymbolImageUrl { get; set; } = "";

    public string SymbolDescription { get; set; } = "";
}