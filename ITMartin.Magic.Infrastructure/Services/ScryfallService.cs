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
    private readonly ICardMatchScoringService _matchScoringService;
    private readonly ISetSymbolMatchingService _setSymbolMatchingService;

    public ScryfallService(
        HttpClient httpClient, ICardMatchScoringService matchScoringService, ISetSymbolMatchingService setSymbolMatchingService)
    {
        _httpClient =
            httpClient;
        _matchScoringService = matchScoringService;
        _setSymbolMatchingService = setSymbolMatchingService;
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
                $"cards/search?q={Uri.EscapeDataString($"!\"{cardName}\"")}",
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
                .ReadFromJsonAsync<ScryfallSearchResponse>(
                    cancellationToken:
                    cancellationToken);

        if (dto is null ||
            dto.Data.Count == 0)
        {
            return null;
        }

        Console.WriteLine(
            "SCRYFALL MATCH: Exact Name");

        Console.WriteLine(
            $"SCRYFALL MATCHES: {dto.Data.Count}");
        var cards =
            dto.Data
                .Select(CreateCard)
                .ToList();
        cards =
            cards
                .OrderBy(x => x.ReleasedAt)
                .ToList();
        var matches =
            cards
                .Select(x =>
                {
                    var score =
                        analysis is null
                            ? 0
                            : _matchScoringService.CalculateScore(
                                x,
                                analysis);

                    return new ScryfallMatch
                    {
                        Card = x,
                        Score = score,
                            Confidence =
                                Math.Min(
                                    score / 1200m,
                                    1m),

                        ConfidenceLabel =
                            score switch
                            {
                                >= 700 => "Very High",
                                >= 400 => "High",
                                >= 200 => "Medium",
                                _ => "Low"
                            }
                    };
                })
                .OrderByDescending(x =>
                    x.Score)
                .ToList();

        return new CardSearchResult
        {
            BestMatch = matches.FirstOrDefault()?.Card,
            Matches = matches
        };
    }

    private static ScryfallCard
        CreateCard(
            ScryfallCardDto dto)
    {
        return new ScryfallCard
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
            Finishes = dto.Finishes ?? [],
            SetName = dto.Set
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
    private async Task<decimal> CalculateScoreAsync(
        ScryfallCard card,
        MagicCardAnalysisResult analysis, CancellationToken cancellationToken)
    {
        decimal score = 0;
        if (!string.IsNullOrWhiteSpace(
                analysis.Name) &&
            string.Equals(
                analysis.Name,
                card.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }
        else
        {
            return 0;
        }
        if (!string.IsNullOrWhiteSpace(
                analysis.ManaCost) &&
            string.Equals(
                analysis.ManaCost,
                card.ManaCost,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }
        if (!string.IsNullOrWhiteSpace(
                analysis.CardType) &&
            !string.IsNullOrWhiteSpace(
                card.TypeLine) &&
            card.TypeLine.Contains(
                analysis.CardType,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }
        if (!string.IsNullOrWhiteSpace(
                analysis.PowerToughness) &&
            !string.IsNullOrWhiteSpace(
                card.Power) &&
            !string.IsNullOrWhiteSpace(
                card.Toughness))
        {
            var pt =
                $"{card.Power}/{card.Toughness}";

            if (string.Equals(
                    analysis.PowerToughness,
                    pt,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 75;
            }
        }
        if (!string.IsNullOrWhiteSpace(
                analysis.Artist) &&
            string.Equals(
                analysis.Artist,
                card.Artist,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber) &&
            string.Equals(
                analysis.CollectorNumber,
                card.CollectorNumber,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }

        if (analysis.WhiteBorder &&
            string.Equals(
                card.BorderColor,
                "white",
                StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (analysis.OldBorder &&
            card.Frame == "1993")
        {
            score += 50;
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.CopyrightYear) &&
            !string.IsNullOrWhiteSpace(
                card.ReleasedAt) &&
            card.ReleasedAt.Contains(
                analysis.CopyrightYear,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.Rarity) &&
            string.Equals(
                analysis.Rarity,
                card.Rarity,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }
        var symbolScore =
            await _setSymbolMatchingService
                .MatchAsync(
                    analysis.VisibleSetSymbolDescription,
                    card.Set,
                    card.SetName,
                    cancellationToken);

        score += symbolScore;

        return score;
    }
}