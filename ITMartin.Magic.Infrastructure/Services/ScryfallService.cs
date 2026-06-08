using System.Net.Http.Json;
using ITMartin.Ai.Models;
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
            MagicCardAnalysisResult? analysis,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return null;
        }

        Console.WriteLine(
            $"AI Name: [{cardName}]");

        Console.WriteLine(
            $"AI Artist: [{analysis?.Artist}]");

        Console.WriteLine(
            $"AI Collector: [{analysis?.CollectorNumber}]");

        Console.WriteLine(
            $"AI Symbol: [{analysis?.VisibleSetSymbolDescription}]");

        Console.WriteLine(
            $"AI White Border: [{analysis?.WhiteBorder}]");

        Console.WriteLine(
            $"AI Old Border: [{analysis?.OldBorder}]");

        var response =
            await _httpClient.GetAsync(
                $"cards/named?exact={Uri.EscapeDataString(cardName)}",
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
            "SCRYFALL MATCH: Exact Name");

        Console.WriteLine(
            $"SCRYFALL CARD: [{dto.Name}] [{dto.Set}] [{dto.CollectorNumber}]");

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
                    ConfidenceLabel = "Exact Name"
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