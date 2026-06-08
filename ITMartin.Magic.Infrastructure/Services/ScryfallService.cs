using System.Net.Http.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class ScryfallService
    : IScryfallService
{
    private readonly HttpClient
        _httpClient;

    public ScryfallService(
        HttpClient httpClient)
    {
        _httpClient =
            httpClient;
    }

    public async Task<CardSearchResult?>
        SearchAsync(
            string? cardName,
            string? setCode,
            string? collectorNumber,
            
        CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(cardName))
    {
        return null;
    }

    Console.WriteLine(
        $"AI Name: [{cardName}]");

    Console.WriteLine(
        $"AI Set: [{setCode}]");

    Console.WriteLine(
        $"AI Collector: [{collectorNumber}]");

    // ==================================================
    // 1. Exact printing lookup (Set + Collector Number)
    // ==================================================

    if (!string.IsNullOrWhiteSpace(setCode)
        && !string.IsNullOrWhiteSpace(collectorNumber))
    {
        var exactResponse =
            await _httpClient.GetAsync(
                $"cards/{setCode.ToLowerInvariant()}/{collectorNumber}",
                cancellationToken);

        if (exactResponse.IsSuccessStatusCode)
        {
            var exactDto =
                await exactResponse.Content
                    .ReadFromJsonAsync<ScryfallCardDto>(
                        cancellationToken:
                        cancellationToken);

            if (exactDto is not null)
            {
                if (!string.Equals(
                        exactDto.Name,cardName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"Collector number mismatch. " +
                        $"AI=[{cardName}] " +
                        $"SCRYFALL=[{exactDto.Name}]");

                    exactDto = null;
                }
            }

            if (exactDto is not null)
            {
                Console.WriteLine(
                    "SCRYFALL MATCH: Exact Printing");

                return CreateResult(exactDto);
            }
        }
    }

    // ==================================================
    // 2. Name + Set lookup
    // ==================================================

    if (!string.IsNullOrWhiteSpace(
            setCode))
    {
        var searchResponse =
            await _httpClient.GetAsync(
                $"cards/search?q=!\"{Uri.EscapeDataString(cardName)}\"+set:{setCode.ToLowerInvariant()}",
                cancellationToken);

        if (searchResponse.IsSuccessStatusCode)
        {
            var searchResult =
                await searchResponse.Content
                    .ReadFromJsonAsync<ScryfallSearchResponseDto>(
                        cancellationToken:
                        cancellationToken);
            
            var card =
                searchResult?.Data?
                    .FirstOrDefault();

            if (card is not null)
            {
                Console.WriteLine(
                    "SCRYFALL MATCH: Name + Set");

                Console.WriteLine(
                    $"SCRYFALL CARD: [{card.Name}] [{card.Set}] [{card.CollectorNumber}]");

                return CreateResult(card);
            }
        }
    }

    // ==================================================
    // 3. Fuzzy name fallback
    // ==================================================

    var name =
        cardName.Trim();

    var response =
        await _httpClient.GetAsync(
            $"cards/named?fuzzy={Uri.EscapeDataString(name)}",
            cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var error =
            await response.Content
                .ReadAsStringAsync(cancellationToken);

        Console.WriteLine(
            $"Scryfall error: {error}");

        return null;
    }

    var dto =
        await response.Content
            .ReadFromJsonAsync<ScryfallCardDto>(
                cancellationToken:
                cancellationToken);

    if (dto is null)
    {
        return null;
    }

    Console.WriteLine(
        "SCRYFALL MATCH: Fuzzy Name");

    return CreateResult(dto);
}
    
    private static CardSearchResult
        CreateResult(
            ScryfallCardDto dto)
    {
        var card =
            new ScryfallCard
            {
                Id = dto.Id,
                Name = dto.Name,
                Set = dto.Set,
                CollectorNumber = dto.CollectorNumber,
                ManaCost = dto.ManaCost,
                TypeLine = dto.TypeLine,
                Rarity = dto.Rarity,
                OracleText = dto.OracleText,
                Artist = dto.Artist,
                Frame = dto.Frame,
                BorderColor = dto.BorderColor,
                Power = dto.Power,
                Toughness = dto.Toughness,
                ReleasedAt = dto.ReleasedAt,
                ImageUrl = dto.ImageUris?.Normal ?? "",
                EurPrice = ParsePrice(dto.Prices?.Eur),
                EurFoilPrice = ParsePrice(dto.Prices?.EurFoil),
                UsdPrice = ParsePrice(dto.Prices?.Usd),
                UsdFoilPrice = ParsePrice(dto.Prices?.UsdFoil),
                Finishes = dto.Finishes ?? []
            };

        return new CardSearchResult
        {
            BestMatch = card,
            Matches =
            [
                new ScryfallMatch
                {
                    Card = card,
                    Score = 100,
                    ConfidenceLabel = "Exact"
                }
            ]
        };
    }
    private static decimal?
        ParsePrice(
            string? value)
    {
        return decimal.TryParse(
            value,
            out var price)
            ? price
            : null;
    }
}