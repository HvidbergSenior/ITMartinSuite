// =========================================
// FILE:
// ITMartin.Magic.Infrastructure/Scryfall/ScryfallService.cs
// STEP 1
// =========================================

using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Infrastructure.Scryfall;

public class ScryfallService
    : IScryfallService
{
    private readonly HttpClient _http;

    public ScryfallService(
        HttpClient http)
    {
        _http = http;
    }

    public async Task<ScryfallCard?> SearchAsync(
        CardDetectionResult detection)
    {
        try
        {
            // =====================================
            // DETECTION DEBUG
            // =====================================

            Console.WriteLine("");
            Console.WriteLine("===== DETECTION =====");

            Console.WriteLine(
                $"NAME: {detection.Name}");

            Console.WriteLine(
                $"CONFIDENCE: {detection.Confidence}");

            Console.WriteLine(
                $"TYPE: {detection.CardType}");

            Console.WriteLine(
                $"TYPE LINE: {detection.TypeLine}");

            Console.WriteLine(
                $"MANA: {detection.ManaCost}");

            Console.WriteLine(
                $"ARTIST: {detection.Artist}");

            Console.WriteLine(
                $"PT: {detection.PowerToughness}");

            Console.WriteLine(
                $"OLD BORDER: {detection.IsOldBorder}");

            Console.WriteLine(
                $"WHITE BORDER: {detection.IsWhiteBorder}");

            Console.WriteLine(
                $"HAS SET SYMBOL: {detection.HasSetSymbol}");

            Console.WriteLine(
                $"FINGERPRINT WHITE BORDER: {detection.Fingerprint.WhiteBorder}");

            Console.WriteLine(
                $"FINGERPRINT OLD FRAME: {detection.Fingerprint.OldFrame}");

            Console.WriteLine(
                $"BORDER BRIGHTNESS: {detection.Fingerprint.BorderBrightness}");

            Console.WriteLine("=====================");
            Console.WriteLine("");

            // =====================================
            // VALIDATION
            // =====================================

            if (string.IsNullOrWhiteSpace(
                    detection.Name))
            {
                return null;
            }

            var cleaned =
                CleanName(
                    detection.Name);

            Console.WriteLine(
                $"CARD NAME: [{cleaned}]");

            // =====================================
            // SEARCH
            // =====================================

            var url =
                $"https://api.scryfall.com/cards/search?q=!\"{Uri.EscapeDataString(cleaned)}\"&unique=prints";

            Console.WriteLine(
                $"SCRYFALL SEARCH: {url}");

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            request.Headers.UserAgent.ParseAdd(
                "ITMartinMagicScanner/6.0");

            request.Headers.Accept.ParseAdd(
                "application/json");

            using var response =
                await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"SCRYFALL FAILED: {response.StatusCode}");

                return null;
            }

            var json =
                await response.Content
                    .ReadAsStringAsync();

            using var doc =
                JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(
                    "data",
                    out var data))
            {
                return null;
            }

            var candidates =
                data.EnumerateArray()
                    .Select(ParseCard)
                    .ToList();

            Console.WriteLine(
                $"TOTAL PRINTS: {candidates.Count}");

            if (candidates.Count == 0)
            {
                return null;
            }

            // =====================================
            // SCORE
            // =====================================

            var scored =
                candidates
                    .Select(x => new
                    {
                        Card = x,
                        Score = ScoreCard(
                            x,
                            detection)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

            Console.WriteLine("");
            Console.WriteLine("===== TOP MATCHES =====");

            foreach (var item in scored.Take(15))
            {
                Console.WriteLine(
                    $"MATCH: {item.Card.Name} " +
                    $"[{item.Card.Set}] " +
                    $"#{item.Card.CollectorNumber} " +
                    $"FRAME={item.Card.Frame} " +
                    $"BORDER={item.Card.BorderColor} " +
                    $"PT={item.Card.Power}/{item.Card.Toughness} " +
                    $"SCORE={item.Score}");
            }

            Console.WriteLine("=======================");
            Console.WriteLine("");

            var bestItem =
                scored.First();

            bestItem.Card.MatchScore =
                bestItem.Score;

            Console.WriteLine(
                $"BEST MATCH: " +
                $"{bestItem.Card.Name} " +
                $"[{bestItem.Card.Set}] " +
                $"#{bestItem.Card.CollectorNumber}");

            return bestItem.Card;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"SCRYFALL ERROR: {ex}");

            return null;
        }
    }

    // =========================================
    // SCORE
    // =========================================

    // =========================================
// FILE:
// ITMartin.Magic.Infrastructure/Scryfall/ScryfallService.cs
// REPLACE ENTIRE ScoreCard METHOD
// =========================================

private int ScoreCard(
    ScryfallCard card,
    CardDetectionResult detection)
{
    var score = 0;

    // =====================================
    // NAME
    // =====================================

    if (Normalize(card.Name) ==
        Normalize(detection.Name))
    {
        score += 5000;
    }
    else
    {
        // HARD FAIL
        return -999999;
    }

    // =====================================
    // OLD FRAME
    // =====================================

    if (detection.Fingerprint.OldFrame)
    {
        if (card.Frame == "1993")
        {
            score += 4000;
        }
        else
        {
            score -= 10000;
        }
    }

    // =====================================
    // WHITE BORDER
    // =====================================

    if (detection.Fingerprint.WhiteBorder)
    {
        if (card.BorderColor == "white")
        {
            score += 10000;
        }
        else
        {
            score -= 15000;
        }
    }
    else
    {
        if (card.BorderColor == "black")
        {
            score += 3000;
        }
    }

    // =====================================
    // POWER / TOUGHNESS
    // =====================================

    if (!string.IsNullOrWhiteSpace(
            detection.PowerToughness))
    {
        var pt =
            $"{card.Power}/{card.Toughness}";

        if (Normalize(pt) ==
            Normalize(detection.PowerToughness))
        {
            score += 7000;
        }
        else
        {
            score -= 4000;
        }
    }

    // =====================================
    // ARTIST
    // =====================================

    if (!string.IsNullOrWhiteSpace(
            detection.Artist))
    {
        if (Normalize(card.Artist) ==
            Normalize(detection.Artist))
        {
            score += 6000;
        }
    }

    // =====================================
    // SET SYMBOL LOGIC
    // =====================================

    // NO SYMBOL:
    // alpha/beta/unlimited/revised/4th

    if (!detection.HasSetSymbol)
    {
        if (card.Set is
            "lea" or
            "leb" or
            "2ed" or
            "3ed" or
            "4ed")
        {
            score += 5000;
        }
        else
        {
            score -= 8000;
        }
    }
    else
    {
        // HAS SYMBOL:
        // NOT early core sets

        if (card.Set is
            "lea" or
            "leb" or
            "2ed" or
            "3ed" or
            "4ed")
        {
            score -= 12000;
        }
    }

    // =====================================
    // TYPE LINE
    // =====================================

    if (!string.IsNullOrWhiteSpace(
            detection.TypeLine))
    {
        if (Normalize(card.TypeLine)
            .Contains(
                Normalize(detection.TypeLine)))
        {
            score += 1200;
        }
    }

    // =====================================
    // MANA COST
    // =====================================

    if (!string.IsNullOrWhiteSpace(
            detection.ManaCost))
    {
        if (Normalize(card.ManaCost) ==
            Normalize(detection.ManaCost))
        {
            score += 2000;
        }
    }

    // =====================================
    // CONFIDENCE PENALTY
    // =====================================

    if (detection.Confidence < 0.50m)
    {
        score -= 3000;
    }

    Console.WriteLine(
        $"FINAL SCORE: {card.Name} [{card.Set}] = {score}");

    return score;
}

    // =========================================
    // PARSE
    // =========================================

    private ScryfallCard ParseCard(
        JsonElement root)
    {
        decimal? ParsePrice(
            string value)
        {
            return decimal.TryParse(
                value,
                out var x)
                ? x
                : null;
        }

        string Get(
            string name)
        {
            return root.TryGetProperty(
                name,
                out var prop)
                ? prop.GetString() ?? ""
                : "";
        }

        string image = "";

        if (root.TryGetProperty(
                "image_uris",
                out var images))
        {
            if (images.TryGetProperty(
                    "normal",
                    out var normal))
            {
                image =
                    normal.GetString() ?? "";
            }
        }

        var finishes =
            new List<string>();

        if (root.TryGetProperty(
                "finishes",
                out var finishesJson))
        {
            finishes =
                finishesJson
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .ToList();
        }

        var prices =
            root.TryGetProperty(
                "prices",
                out var p)
                ? p
                : default;

        return new ScryfallCard
        {
            Name = Get("name"),
            Set = Get("set"),
            CollectorNumber = Get("collector_number"),
            ManaCost = Get("mana_cost"),
            TypeLine = Get("type_line"),
            Rarity = Get("rarity"),
            OracleText = Get("oracle_text"),
            Artist = Get("artist"),
            Frame = Get("frame"),
            BorderColor = Get("border_color"),
            ReleasedAt = Get("released_at"),
            ImageUrl = image,
            Finishes = finishes,

            Power = Get("power"),
            Toughness = Get("toughness"),

            EurPrice =
                prices.ValueKind != JsonValueKind.Undefined
                    ? ParsePrice(
                        prices.GetProperty("eur")
                            .GetString() ?? "")
                    : null,

            EurFoilPrice =
                prices.ValueKind != JsonValueKind.Undefined
                    ? ParsePrice(
                        prices.GetProperty("eur_foil")
                            .GetString() ?? "")
                    : null,

            UsdPrice =
                prices.ValueKind != JsonValueKind.Undefined
                    ? ParsePrice(
                        prices.GetProperty("usd")
                            .GetString() ?? "")
                    : null,

            UsdFoilPrice =
                prices.ValueKind != JsonValueKind.Undefined
                    ? ParsePrice(
                        prices.GetProperty("usd_foil")
                            .GetString() ?? "")
                    : null
        };
    }

    // =========================================
    // HELPERS
    // =========================================

    private static string CleanName(
        string text)
    {
        text =
            text
                .Trim()
                .Replace("\n", " ")
                .Replace("\r", " ");

        return Regex.Replace(
            text,
            @"\s+",
            " ");
    }

    private static string Normalize(
        string? text)
    {
        return (text ?? "")
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace("\"", "")
            .Replace("'", "");
    }
}