public sealed class MagicSetKnowledge
{
    public string SetCode { get; set; } = "";

    public string SetName { get; set; } = "";

    public DateTime ReleasedAt { get; set; }

    public bool HasSymbol { get; set; }

    public string BorderColor { get; set; } = "";

    public string FrameStyle { get; set; } = "";

    public string SymbolSvgUrl { get; set; } = "";
}