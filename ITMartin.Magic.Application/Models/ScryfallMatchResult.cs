namespace ITMartin.Magic.Application.Models;

public sealed record ScryfallMatchResult
{
    public required string Name { get; init; }

    public required string SetCode { get; init; }

    public required string CollectorNumber { get; init; }

    public string? ScryfallId { get; init; }

    public string? ImageUrl { get; init; }
}