namespace ITMartin.Magic.Application.Models;

public sealed record CardScanResult
{
    public string? Name { get; init; }

    public string? SetCode { get; init; }

    public string? CollectorNumber { get; init; }

    public string? ImageUrl { get; init; }

    public decimal? EurPrice { get; init; }

    public decimal? UsdPrice { get; init; }
}