using System.Text.Json;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;
using Microsoft.Extensions.Configuration;
namespace ITMartinLibrary.Application.Services;

public class BarcodeLookupService : IBarcodeLookupService
{
    private readonly HttpClient _httpClient;
    private readonly string _omdbKey;

    public BarcodeLookupService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _omdbKey = configuration["ApiKeys:Omdb"] ?? "";
    }

    public async Task<InventoryItem?> LookupAsync(string barcode)
    {
        // Books + ISBN magazines
        if (barcode.StartsWith("978") || barcode.StartsWith("979"))
            return await LookupBookAsync(barcode);

        // For non-book items we do lightweight detection
        return await LookupMediaFallbackAsync(barcode);
    }

    private async Task<InventoryItem?> LookupBookAsync(string barcode)
    {
        var url =
            $"https://openlibrary.org/api/books?bibkeys=ISBN:{barcode}&format=json&jscmd=data";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var key = $"ISBN:{barcode}";

        if (!doc.RootElement.TryGetProperty(key, out var book))
            return null;

        string? title = book.GetProperty("title").GetString();

        string? author = null;

        if (book.TryGetProperty("authors", out var authors) &&
            authors.GetArrayLength() > 0)
        {
            author = authors[0].GetProperty("name").GetString();
        }

        return new InventoryItem
        {
            Barcode = barcode,
            Title = title,
            AuthorOrDirector = author,
            Type = "Book"
        };
    }

    private async Task<InventoryItem?> LookupMediaFallbackAsync(string barcode)
    {
        // free fallback:
        // save barcode and let user edit later
        return new InventoryItem
        {
            Barcode = barcode,
            Title = $"Scanned item {barcode}",
            Type = GuessTypeFromBarcode(barcode),
            LookupStatus = "Pending Manual Review",
            DetailsUpdatedAt = DateTime.UtcNow
        };
    }

    private string GuessTypeFromBarcode(string barcode)
    {
        if (barcode.Length == 13)
            return "DVD / Blu-ray / Magazine";

        if (barcode.Length == 12)
            return "DVD / Merchandise";

        return "Unknown";
    }

    public async Task<string?> LookupMovieDirectorAsync(string title)
    {
        if (string.IsNullOrWhiteSpace(_omdbKey))
            return null;

        var url =
            $"https://www.omdbapi.com/?apikey={_omdbKey}&t={Uri.EscapeDataString(title)}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("Director", out var director))
            return null;

        return director.GetString();
    }
}