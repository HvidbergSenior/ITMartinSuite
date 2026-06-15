namespace ITMartin.Ai.Models;

public sealed record LibraryShelfItem
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Isbn { get; init; }

    public string? Barcode { get; init; }

    public string? MediaType { get; init; }

    public decimal Confidence { get; init; }
}