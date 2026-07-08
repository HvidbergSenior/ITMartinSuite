using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class CardMatchScoringService
    : ICardMatchScoringService
{
    private static readonly HashSet<string> OldFrameSets =
        new(StringComparer.OrdinalIgnoreCase) { "lea", "leb", "2ed", "3ed", "4ed", "4bb" };

    private readonly ILogger<CardMatchScoringService> _logger;

    public CardMatchScoringService(ILogger<CardMatchScoringService> logger)
    {
        _logger = logger;
    }

    public decimal CalculateScore(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        decimal score = 0;

        _logger.LogDebug("Scoring [{Name}] [{Set}]", card.Name, card.Set);

        if (!string.Equals(
                analysis.IdentifiedName,
                card.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Name mismatch — score 0");

            return 0;
        }

        score += 100;

        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            if (string.Equals(
                    analysis.CollectorNumber,
                    card.CollectorNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }
            else
            {
                score -= 500;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.Artist))
        {
            if (string.Equals(
                    analysis.Artist,
                    card.Artist,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 300;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.ManaCost))
        {
            if (string.Equals(
                    analysis.ManaCost,
                    card.ManaCost,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.CardType))
        {
            if (card.TypeLine.Contains(
                    analysis.CardType,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.PowerToughness))
        {
            if ($"{card.Power}/{card.Toughness}" ==
                analysis.PowerToughness)
            {
                score += 100;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.BorderColor))
        {
            if (string.Equals(
                    analysis.BorderColor,
                    card.BorderColor,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 200;
            }
            else
            {
                score -= 300;
            }
        }

        if (!string.IsNullOrWhiteSpace(analysis.CopyrightYear) &&
            !string.IsNullOrWhiteSpace(card.ReleasedAt) &&
            card.ReleasedAt.Length >= 4)
        {
            if (card.ReleasedAt[..4] == analysis.CopyrightYear)
            {
                score += 150;
            }
            else
            {
                score -= 150;
            }
        }

        // Old-frame reprints (Alpha–4th Edition) often share identical border color and
        // can have illegible copyright text — this structural detail (nothing vs. something
        // printed under the artist credit) reliably tells Revised apart from 4th Edition
        // even when the year can't be read.
        if (analysis.HasLineUnderArtist.HasValue &&
            OldFrameSets.Contains(card.Set))
        {
            var isRevised = string.Equals(card.Set, "3ed", StringComparison.OrdinalIgnoreCase);
            var noLine = !analysis.HasLineUnderArtist.Value;

            score += (noLine == isRevised) ? 150 : -150;
        }

        _logger.LogDebug("Final score for [{Name}] [{Set}]: {Score}", card.Name, card.Set, score);

        return score;
    }
}