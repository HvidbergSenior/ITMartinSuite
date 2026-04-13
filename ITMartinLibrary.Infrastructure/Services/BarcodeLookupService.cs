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
        // Books
        if (barcode.StartsWith("978") || barcode.StartsWith("979"))
        {
            var book = await LookupGoogleBooksAsync(barcode);

            if (book is not null)
                return book;
        }

        // DVD / Blu-ray manual fallback not barcode-direct
        return null;
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
}