namespace ITMartin.Magic.Domain.Entities;

public sealed class MagicCard
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string SetCode { get; init; }

    public required string CollectorNumber { get; init; }

    // Whose collection this card belongs to — a free-typed name, not a real
    // account (no auth system exists). Empty means unattributed/legacy data
    // scanned before this field existed.
    public string Owner { get; set; } = "";

    public string? ScryfallId { get; set; }

    public int Quantity { get; set; }

    public decimal? EurPrice { get; set; }

    public decimal? UsdPrice { get; set; }

    public DateTime FirstSeenAt { get; init; }

    public DateTime LastSeenAt { get; set; }
}