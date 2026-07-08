namespace ITMartin.Magic.Application.Models;

public sealed record ScryfallMatchResult
{
    public required string Name { get; init; }

    public required string SetCode { get; init; }

    public string? SetName { get; init; }

    public required string CollectorNumber { get; init; }

    public string? ScryfallId { get; init; }

    public string? ImageUrl { get; init; }

    public decimal? EurPrice { get; init; }

    public decimal? EurFoilPrice { get; init; }

    public decimal? UsdPrice { get; init; }

    public decimal? UsdFoilPrice { get; init; }
    public List<CardCandidateViewModel>
        Candidates { get; set; } = [];

    // True when another printing scored the same as the best match — the AI
    // didn't extract enough distinguishing detail (usually copyright year)
    // to actually tell them apart, so this pick is a guess, not a confirmed match.
    public bool IsAmbiguous { get; init; }
}