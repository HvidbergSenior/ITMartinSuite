public sealed class CardMatchCandidate
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string SetCode { get; set; } = "";

    public string SetName { get; set; } = "";

    public string? CollectorNumber { get; set; }

    public string? Artist { get; set; }

    public string? Rarity { get; set; }

    public string? ScryfallImageUrl { get; set; }

    public decimal? EurPrice { get; set; }

    public decimal Score { get; set; }

    public decimal Confidence { get; set; }
}