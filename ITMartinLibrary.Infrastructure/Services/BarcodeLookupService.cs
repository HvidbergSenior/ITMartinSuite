using System.Text.Json;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;
using ITMartinLibrary.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeLookupService : IBarcodeLookupService
{
    private readonly HttpClient _httpClient;
    private readonly OmdbOptions _omdbOptions;

    public BarcodeLookupService(
        HttpClient httpClient,
        IOptions<OmdbOptions> omdbOptions)
    {
        _httpClient = httpClient;
        _omdbOptions = omdbOptions.Value;
    }

    public async Task<InventoryItem?> LookupAsync(string barcode)
    {
        Console.WriteLine($"LOOKUP START: {barcode}");
        Console.WriteLine($"IS BOOK: {barcode.StartsWith("978") || barcode.StartsWith("979")}");
        if (barcode.StartsWith("978") || barcode.StartsWith("979"))
        {
            Console.WriteLine("TRYING GOOGLE BOOKS");
            var book = await LookupGoogleBooksAsync(barcode);

            if (book is not null)
            {
                Console.WriteLine($"BOOK FOUND: {book.Title} / {book.Type}");
                return book;
            }
        }

        var movie = await LookupUpcItemDbAsync(barcode);

        if (movie is not null)
            return movie;

        return new InventoryItem
        {
            Title = "Pending manual review",
            Type = barcode.StartsWith("978") || barcode.StartsWith("979")
                ? "Book"
                : "DVD"
        };
    }

    public async Task<InventoryItem?> LookupMovieByTitleAsync(string title)
    {
        var apiKey = _omdbOptions.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var url =
            $"https://www.omdbapi.com/?apikey={apiKey}&t={Uri.EscapeDataString(title)}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("Title", out var titleProp))
            return null;

        var director = document.RootElement.TryGetProperty("Director", out var directorProp)
            ? directorProp.GetString()
            : "";

        var genre = document.RootElement.TryGetProperty("Genre", out var genreProp)
            ? genreProp.GetString()
            : "";

        var runtime = document.RootElement.TryGetProperty("Runtime", out var runtimeProp)
            ? runtimeProp.GetString()
            : "";

        var year = document.RootElement.TryGetProperty("Year", out var yearProp)
            ? yearProp.GetString()
            : "";

        var plot = document.RootElement.TryGetProperty("Plot", out var plotProp)
            ? plotProp.GetString()
            : "";

        var poster = document.RootElement.TryGetProperty("Poster", out var posterProp)
            ? posterProp.GetString()
            : "";

        return new InventoryItem
        {
            Title = titleProp.GetString() ?? "",
            AuthorOrDirector = director ?? "",
            Genre = genre ?? "",
            Runtime = runtime ?? "",
            ReleaseYear = year ?? "",
            Plot = plot ?? "",
            CoverUrl = poster ?? "",
            Type = "DVD"
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

        if (!document.RootElement.TryGetProperty("items", out var items) ||
            items.GetArrayLength() == 0)
            return null;

        var volumeInfo = items[0].GetProperty("volumeInfo");

        var title = volumeInfo.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString()
            : "";

        return new InventoryItem
        {
            Title = title ?? "",
            Type = "Book"
        };
    }
    
    private async Task<InventoryItem?> LookupUpcItemDbAsync(string barcode)
    {
        var url =
            $"https://api.upcitemdb.com/prod/trial/lookup?upc={barcode}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("items", out var items) ||
            items.GetArrayLength() == 0)
            return null;

        var item = items[0];

        var title = item.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString()
            : "";

        if (string.IsNullOrWhiteSpace(title))
            return null;

        // enrich with OMDb
        var movie = await LookupMovieByTitleAsync(title);

        if (movie is not null)
            return movie;

        return new InventoryItem
        {
            Title = title,
            Type = "DVD"
        };
    }
}