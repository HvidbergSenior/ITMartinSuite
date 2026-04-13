using System.Text.Json;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeLookupService : IBarcodeLookupService
{
    private readonly HttpClient _httpClient;

    public BarcodeLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<InventoryItem?> LookupAsync(string barcode)
    {
        // Try OpenLibrary first
        var result = await LookupOpenLibraryAsync(barcode);

        if (result is not null)
            return result;

        // Fallback to Google Books
        result = await LookupGoogleBooksAsync(barcode);

        return result;
    }

    private async Task<InventoryItem?> LookupOpenLibraryAsync(string barcode)
    {
        var url =
            $"https://openlibrary.org/api/books?bibkeys=ISBN:{barcode}&format=json&jscmd=data";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var key = $"ISBN:{barcode}";

        if (!document.RootElement.TryGetProperty(key, out var book))
            return null;

        var title = book.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString()
            : null;

        string? author = null;

        if (book.TryGetProperty("authors", out var authors) &&
            authors.ValueKind == JsonValueKind.Array &&
            authors.GetArrayLength() > 0)
        {
            author = authors[0].GetProperty("name").GetString();
        }

        return new InventoryItem
        {
            Title = title ?? "Unknown Book",
            AuthorOrDirector = author,
            Type = "Book"
        };
    }

    private async Task<InventoryItem?> LookupGoogleBooksAsync(string barcode)
    {
        var url =
            $"https://www.googleapis.com/books/v1/volumes?q=isbn:{barcode}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("items", out var items))
            return null;

        if (items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() == 0)
            return null;

        var volumeInfo = items[0].GetProperty("volumeInfo");

        var title = volumeInfo.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString()
            : null;

        string? author = null;

        if (volumeInfo.TryGetProperty("authors", out var authors) &&
            authors.ValueKind == JsonValueKind.Array &&
            authors.GetArrayLength() > 0)
        {
            author = authors[0].GetString();
        }

        return new InventoryItem
        {
            Title = title ?? "Unknown Book",
            AuthorOrDirector = author,
            Type = "Book"
        };
    }
}