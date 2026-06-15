namespace ITMartin.Library.Application.Models;

public sealed class LibraryItem
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "";

    public string MediaType { get; set; } = "";

    public string? Author { get; set; }

    public string? Barcode { get; set; }

    public string? Isbn { get; set; }

    public decimal Confidence { get; set; }

    public string? ImageUrl { get; set; }
}