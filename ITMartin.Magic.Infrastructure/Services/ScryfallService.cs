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
    private readonly IPrintingEliminationService _printingEliminationService;

    public ScryfallService(
        HttpClient httpClient, ICardMatchScoringService matchScoringService,
        ISetSymbolMatchingService setSymbolMatchingService, IPrintingEliminationService printingEliminationService)
    {
        _httpClient =
            httpClient;
        _matchScoringService = matchScoringService;
        _setSymbolMatchingService = setSymbolMatchingService;
        _printingEliminationService = printingEliminationService;
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

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("SCRYFALL SEARCH");
        Console.WriteLine("========================================");

        Console.WriteLine(
            $"AI Card: [{analysis?.IdentifiedName}]");

        Console.WriteLine(
            $"AI Mana Cost: [{analysis?.ManaCost}]");

        Console.WriteLine(
            $"AI Type: [{analysis?.CardType}]");

        Console.WriteLine(
            $"AI P/T: [{analysis?.PowerToughness}]");

        Console.WriteLine(
            $"AI Artist: [{analysis?.Artist}]");

        Console.WriteLine(
            $"AI Collector: [{analysis?.CollectorNumber}]");

        Console.WriteLine(
            $"AI Border: [{analysis?.OuterBorder}]");

        Console.WriteLine(
            $"AI Frame: [{analysis?.FrameColor}]");

        Console.WriteLine(
            $"AI Frame Style: [{analysis?.FrameStyle}]");

        Console.WriteLine(
            $"AI Symbol: [{analysis?.SetSymbolDescription}]");

        Console.WriteLine(
            $"AI Confidence: [{analysis?.IdentificationConfidence}]");
        Console.WriteLine(
            $"CopyrightText: [{analysis?.CopyrightText}]");
        Console.WriteLine(
            $"CopyrightTextColor: [{analysis?.CopyrightTextColor}]");

        Console.WriteLine();
        var response =
            await _httpClient.GetAsync(
                $"cards/search?q={Uri.EscapeDataString($"name:\"{cardName}\"")}",
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
            await _printingEliminationService
                .EliminateAsync(
                    cards,
                    analysis,
                    cancellationToken);
        
        var matches =
            cards
                .Select(card =>
                {
                    if (analysis is not null &&
                        !PassesHardFilters(
                            card,
                            analysis))
                    {
                        return new ScryfallMatch
                        {
                            Card = card,
                            Score = 0,
                            Confidence = 0
                        };
                    }

                    var score =
                        analysis is null
                            ? 0
                            : _matchScoringService.CalculateScore(
                                card,
                                analysis);

                    return new ScryfallMatch
                    {
                        Card = card,
                        Score = score,
                        Confidence = Math.Min(score / 1500m, 1m)
                    };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
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
        MagicCardAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        decimal score = 0;

        if (!string.Equals(
                analysis.IdentifiedName,
                card.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        score += 1000;

        if (!string.IsNullOrWhiteSpace(analysis.ManaCost) &&
            string.Equals(
                analysis.ManaCost,
                card.ManaCost,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(analysis.CardType) &&
            card.TypeLine.Contains(
                analysis.CardType,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(analysis.PowerToughness) &&
            $"{card.Power}/{card.Toughness}" ==
            analysis.PowerToughness)
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(analysis.Artist) &&
            string.Equals(
                analysis.Artist,
                card.Artist,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 250;
        }

        if (!string.IsNullOrWhiteSpace(analysis.CollectorNumber) &&
            string.Equals(
                analysis.CollectorNumber,
                card.CollectorNumber,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        if (!string.IsNullOrWhiteSpace(analysis.OuterBorder))
        {
            if (analysis.OuterBorder.Contains(
                    "white",
                    StringComparison.OrdinalIgnoreCase) &&
                card.BorderColor == "white")
            {
                score += 100;
            }

            if (analysis.OuterBorder.Contains(
                    "black",
                    StringComparison.OrdinalIgnoreCase) &&
                card.BorderColor == "black")
            {
                score += 100;
            }
        }

        if (!string.IsNullOrWhiteSpace(analysis.FrameStyle))
        {
            if (analysis.FrameStyle.Contains(
                    "old",
                    StringComparison.OrdinalIgnoreCase) &&
                card.Frame == "1993")
            {
                score += 100;
            }
        }

        var symbolScore =
            await _setSymbolMatchingService.MatchAsync(
                analysis.SetSymbolDescription,
                card.Set,
                card.SetName,
                cancellationToken);

        score += symbolScore;

        return score;
    }
    
    private static bool PassesHardFilters(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        if (!string.IsNullOrWhiteSpace(
                analysis.OuterBorder))
        {
            if (analysis.OuterBorder.Equals(
                    "White",
                    StringComparison.OrdinalIgnoreCase) &&
                !card.BorderColor.Equals(
                    "white",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (analysis.OuterBorder.Equals(
                    "Black",
                    StringComparison.OrdinalIgnoreCase) &&
                !card.BorderColor.Equals(
                    "black",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            if (!string.Equals(
                    analysis.CollectorNumber,
                    card.CollectorNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}