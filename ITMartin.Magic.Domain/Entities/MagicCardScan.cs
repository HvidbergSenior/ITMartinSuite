namespace ITMartin.Magic.Domain.Entities;

public sealed class MagicCardScan
{
    public Guid Id { get; init; }

    public required string OriginalImagePath { get; init; }

    public string? CardName { get; set; }

    public string? SetCode { get; set; }

    public string? CollectorNumber { get; set; }

    public string? ScryfallId { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? EurPrice { get; set; }

    public decimal? UsdPrice { get; set; }

    public string? Condition { get; set; }

    public DateTime CreatedAt { get; init; }
}