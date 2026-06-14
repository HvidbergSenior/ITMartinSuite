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
    private readonly IPrintingEliminationService _printingEliminationService;

    public ScryfallService(
        HttpClient httpClient, ICardMatchScoringService matchScoringService, IPrintingEliminationService printingEliminationService)
    {
        _httpClient =
            httpClient;
        _matchScoringService = matchScoringService;
        _printingEliminationService = printingEliminationService;
    }

    public async Task<CardSearchResult?> SearchAsync(
        string? cardName,
        string? setCode,
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
            $"AI Confidence: [{analysis?.IdentificationConfidence}]");

        var query =
            $"!\"{cardName}\"";

        var url =
            $"cards/search?q={Uri.EscapeDataString(query)}&unique=prints";
        
        Console.WriteLine($"URL: {url}");
        
        
        var response =
            await _httpClient.GetAsync(
                url,
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
        
        if (string.IsNullOrWhiteSpace(setCode))
        {
            cards =
                cards
                    .Where(x =>
                        x.Set is
                            "lea" or
                            "leb" or
                            "2ed" or
                            "3ed" or
                            "4ed" or
                            "arn" or
                            "atq" or
                            "leg" or
                            "drk" or
                            "fem" or
                            "ice" or
                            "chr" or
                            "rin")
                    .ToList();

            Console.WriteLine(
                $"NO SET SELECTED FILTER: {cards.Count}");
        }
        Console.WriteLine("ALL PRINTINGS:");

        foreach (var card in cards)
        {
            Console.WriteLine(
                $"{card.Name} [{card.Set}] #{card.CollectorNumber}");
        }
        if (!string.IsNullOrWhiteSpace(setCode))
        {
            var filteredCards =
                cards
                    .Where(x =>
                        string.Equals(
                            x.Set,
                            setCode,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            Console.WriteLine(
                $"SET FILTER: {setCode}");

            Console.WriteLine(
                $"MATCHES AFTER FILTER: {filteredCards.Count}");

            if (filteredCards.Count > 0)
            {
                cards = filteredCards;
            }
        }
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
                        Confidence = analysis?.IdentificationConfidence ?? 0
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
    
    
    private static bool PassesHardFilters(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            return string.Equals(
                analysis.CollectorNumber,
                card.CollectorNumber,
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}