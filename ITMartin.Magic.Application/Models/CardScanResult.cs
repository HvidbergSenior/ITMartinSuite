namespace ITMartin.Magic.Application.Models;

public sealed record CardScanResult
{
    public string? Name { get; init; }

    public string? SetCode { get; init; }

    public string? CollectorNumber { get; init; }

    public string? ScryfallId { get; init; }

    public string? ImageUrl { get; init; }

    public decimal? EurPrice { get; init; }

    public decimal? UsdPrice { get; init; }

    public string? Condition { get; init; }

    public decimal? AdjustedEurValue { get; init; }

    public decimal? AdjustedUsdValue { get; init; }

    public decimal Confidence { get; init; }

    public bool IsBlurry { get; init; }

    public string? NormalizedImagePath { get; init; }
}