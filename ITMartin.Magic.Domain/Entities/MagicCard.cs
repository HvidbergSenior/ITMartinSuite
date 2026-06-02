namespace ITMartin.Magic.Domain.Entities;

public sealed class MagicCard
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string SetCode { get; init; }

    public required string CollectorNumber { get; init; }

    public string? ScryfallId { get; set; }

    public int Quantity { get; set; }

    public decimal? EurPrice { get; set; }

    public decimal? UsdPrice { get; set; }

    public DateTime FirstSeenAt { get; init; }

    public DateTime LastSeenAt { get; set; }
}