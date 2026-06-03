namespace ITMartin.Magic.Application.Models;

public sealed record ScryfallMatchResult
{
    public required string Name { get; init; }

    public required string SetCode { get; init; }

    public required string CollectorNumber { get; init; }

    public string? ScryfallId { get; init; }

    public string? ImageUrl { get; init; }

    public decimal? EurPrice { get; init; }

    public decimal? EurFoilPrice { get; init; }

    public decimal? UsdPrice { get; init; }

    public decimal? UsdFoilPrice { get; init; }
    public List<CardCandidateViewModel>
        Candidates { get; set; } = [];
}