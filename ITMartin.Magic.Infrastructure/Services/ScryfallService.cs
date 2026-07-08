using System.Net.Http.Json;
using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class ScryfallService
    : IScryfallService
{
    private readonly HttpClient
        _httpClient;

    private readonly ICardMatchScoringService _matchScoringService;
    private readonly IPrintingEliminationService _printingEliminationService;
    private readonly ILogger<ScryfallService> _logger;

    public ScryfallService(
        HttpClient httpClient,
        ICardMatchScoringService matchScoringService,
        IPrintingEliminationService printingEliminationService,
        ILogger<ScryfallService> logger)
    {
        _httpClient =
            httpClient;
        _matchScoringService = matchScoringService;
        _printingEliminationService = printingEliminationService;
        _logger = logger;
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

        _logger.LogDebug(
            "Scryfall search — Card: [{Name}] ManaCost: [{ManaCost}] Type: [{Type}] P/T: [{PT}] Artist: [{Artist}] Collector: [{Collector}] Confidence: [{Confidence}]",
            analysis?.IdentifiedName,
            analysis?.ManaCost,
            analysis?.CardType,
            analysis?.PowerToughness,
            analysis?.Artist,
            analysis?.CollectorNumber,
            analysis?.IdentificationConfidence);

        var query =
            $"!\"{cardName}\"";

        var url =
            $"cards/search?q={Uri.EscapeDataString(query)}&unique=prints";
        
        _logger.LogDebug("Scryfall URL: {Url}", url);
        
        
        var response =
            await _httpClient.GetAsync(
                url,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content
                    .ReadAsStringAsync(cancellationToken);

            _logger.LogWarning("Scryfall error: {Error}", error);

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
        _logger.LogDebug("Scryfall exact name match — {Count} printings", dto.Data.Count);
      
        var cards =
            dto.Data
                .Select(CreateCard)
                .ToList();
        
        if (string.IsNullOrWhiteSpace(setCode))
        {
            // No set specified = card has no set symbol; restrict to sets that printed cards without one
            cards =
                cards
                    .Where(x =>
                        x.Set is
                            "lea" or  // Alpha
                            "leb" or  // Beta
                            "2ed" or  // Unlimited
                            "3ed" or  // Revised
                            "4ed" or  // 4th Edition
                            "4bb" or  // 4th Edition Black Border (foreign)
                            "arn" or  // Arabian Nights
                            "atq" or  // Antiquities
                            "5ed" or  // 5th Edition
                            "chr" or  // Chronicles
                            "ren")    // Renaissance (foreign)
                    .ToList();

            _logger.LogDebug("No-set filter applied — {Count} printings remain", cards.Count);
        }
        foreach (var card in cards)
        {
            _logger.LogDebug("Printing: {Name} [{Set}] #{Collector}", card.Name, card.Set, card.CollectorNumber);
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

            _logger.LogDebug("Set filter [{SetCode}] — {Count} printings remain", setCode, filteredCards.Count);

            if (filteredCards.Count > 0)
            {
                cards = filteredCards;
            }
        }

        // Prefer English printings — foreign-language reprints (e.g. "4bb" Fourth
        // Edition Foreign Black Border) otherwise tie with the correct English
        // printing whenever the AI didn't extract enough distinguishing detail.
        var englishOnly =
            cards.Where(x => string.Equals(x.Lang, "en", StringComparison.OrdinalIgnoreCase)).ToList();

        if (englishOnly.Count > 0)
        {
            cards = englishOnly;
        }

        if (analysis is not null)
        {
            cards =
                await _printingEliminationService
                    .EliminateAsync(
                        cards,
                        analysis,
                        cancellationToken);
        }
        
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
                            ? 100 // name already matched via the Scryfall query itself
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

    public async Task<(decimal? Eur, decimal? Usd)?> GetPriceByIdAsync(
        string scryfallId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"cards/{scryfallId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var dto = await response.Content.ReadFromJsonAsync<ScryfallCardDto>(cancellationToken: cancellationToken);
            if (dto is null) return null;
            return (ParsePrice(dto.Prices?.Eur), ParsePrice(dto.Prices?.Usd));
        }
        catch
        {
            return null;
        }
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
            SetName = dto.SetName,
            Lang = dto.Lang
        };
    }

    private static decimal?
        ParsePrice(
            string? value)
    {
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
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