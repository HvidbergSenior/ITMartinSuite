using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenAiMagicCardAnalysisService
    : IMagicCardAnalysisService
{
    private readonly ChatClient _client;

    private static readonly
        ConcurrentDictionary<string, MagicCardAnalysisResult>
        MagicCache = new();

    private static readonly
        ConcurrentDictionary<string, CardConditionResult>
        ConditionCache = new();

    public OpenAiMagicCardAnalysisService(
        IConfiguration configuration)
    {
        var apiKey =
            configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception(
                "Missing OpenAI API key");
        }

        _client = new ChatClient(
            model: "gpt-4.1-mini",
            apiKey: apiKey);
    }

    // =========================================
    // MAGIC CARD ANALYSIS
    // =========================================

    public async Task<MagicCardAnalysisResult?>
        AnalyzeMagicCardAsync(
            string filePath,
            CardDetectionResult detection)
    {
        try
        {
            // =====================================
            // IMAGE
            // =====================================

            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

            var cacheKey =
                CreateHash(bytes);

            // =====================================
            // CACHE
            // =====================================

            if (MagicCache.TryGetValue(
                    $"mtg-v2-{cacheKey}",
                    out var cached))
            {
                return cached;
            }

            var mime =
                GetMimeType(filePath);

            // =====================================
            // PROMPTS
            // =====================================

            var messages =
                new List<ChatMessage>
                {
                    BuildSystemPrompt(),

                    BuildUserPrompt(
                        bytes,
                        mime,
                        detection)
                };

            // =====================================
            // OPTIONS
            // =====================================

            var options =
                new ChatCompletionOptions
                {
                    Temperature = 0,

                    ResponseFormat =
                        ChatResponseFormat
                            .CreateJsonObjectFormat()
                };

            // =====================================
            // REQUEST
            // =====================================

            var response =
                await _client.CompleteChatAsync(
                    messages,
                    options);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

            Console.WriteLine(
                $"MAGIC AI RESPONSE: {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // =====================================
            // DESERIALIZE
            // =====================================

            var result =
                JsonSerializer.Deserialize<
                    MagicCardAnalysisResult>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                return null;
            }

            // =====================================
            // CACHE SAVE
            // =====================================

            MagicCache[
                $"mtg-v2-{cacheKey}"] =
                result;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"MAGIC AI ERROR: {ex}");

            return null;
        }
    }

    // =========================================
    // CARD CONDITION ANALYSIS
    // =========================================

    public async Task<CardConditionResult?>
        AnalyzeCardConditionAsync(
            string filePath,
            decimal? eurPrice,
            decimal? usdPrice)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

            var cacheKey =
                CreateHash(bytes);

            if (ConditionCache.TryGetValue(
                    $"condition-v2-{cacheKey}",
                    out var cached))
            {
                return cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    BuildConditionPrompt(
                        eurPrice,
                        usdPrice),

                    new UserChatMessage(
                    [
                        ChatMessageContentPart
                            .CreateTextPart(
                                "Analyze this Magic card condition"),

                        ChatMessageContentPart
                            .CreateImagePart(
                                BinaryData.FromBytes(bytes),
                                mime,
                                ChatImageDetailLevel.High)
                    ])
                };

            var options =
                new ChatCompletionOptions
                {
                    Temperature = 0,

                    ResponseFormat =
                        ChatResponseFormat
                            .CreateJsonObjectFormat()
                };

            var response =
                await _client.CompleteChatAsync(
                    messages,
                    options);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

            Console.WriteLine(
                $"CONDITION AI RESPONSE: {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var result =
                JsonSerializer.Deserialize<
                    CardConditionResult>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                return null;
            }

            result.AdjustedEurValue =
                eurPrice *
                result.EstimatedValueMultiplier;

            result.AdjustedUsdValue =
                usdPrice *
                result.EstimatedValueMultiplier;

            ConditionCache[
                $"condition-v2-{cacheKey}"] =
                result;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"CONDITION AI ERROR: {ex}");

            return null;
        }
    }

    // =========================================
    // SYSTEM PROMPT
    // =========================================

    private static SystemChatMessage
        BuildSystemPrompt()
    {
        return new SystemChatMessage("""
                                     You are an expert Magic The Gathering
                                     printing identification system.

                                     Identify the EXACT Magic card printing.

                                     IMPORTANT:
                                     Many old MTG cards do NOT contain
                                     set symbols.

                                     Return ONLY valid JSON.

                                     Use EXACTLY this schema:

                                     {
                                     "name": string|null,
                                     "artist": string|null,
                                     "setCode": string|null,
                                     "collectorNumber": string|null,
                                     "oldBorder": boolean,
                                     "whiteBorder": boolean,
                                     "powerToughness": string|null,
                                     "manaCost": string|null,
                                     "cardType": string|null,
                                     "rarity": string|null,
                                     "confidence": number,
                                     "exactPrintingCertain": boolean
                                     }

                                     Rules:

                                     * ALWAYS use "name"
                                     * NEVER use "title"
                                     * NEVER add extra properties
                                     * Use null if uncertain
                                     * Return ONLY JSON

                                     Priority order:

                                     1. Collector number
                                     2. Set symbol
                                     3. Border color
                                     4. Copyright line
                                     5. Artist
                                     6. Card frame
                                     7. Mana cost
                                     8. Layout era
                                     9. Rarity

                                     Very important:

                                     * white border is NOT Alpha/Beta
                                     * Alpha/Beta are black bordered
                                     * old cards often have ambiguous printings
                                     """);
    }


    // =========================================
    // USER PROMPT
    // =========================================

    private static UserChatMessage
        BuildUserPrompt(
            byte[] bytes,
            string mime,
            CardDetectionResult detection)
    {
        return new UserChatMessage(
        [
            ChatMessageContentPart
                .CreateTextPart(
                    $"""
                    Analyze this Magic card.

                    OCR RESULTS:

                    Title:
                    {detection.Name}

                    Artist:
                    {detection.Artist}

                    Set:
                    {detection.SetCode}

                    Collector:
                    {detection.CollectorNumber}

                    Copyright:
                    {detection.Copyright}

                    PT:
                    {detection.PowerToughness}

                    Old Border:
                    {detection.IsOldBorder}

                    White Border:
                    {detection.IsWhiteBorder}

                    OCR Confidence:
                    {detection.OcrConfidence}

                    OCR Debug:
                    {detection.OcrDebugText}

                    IMPORTANT:
                    OCR and border detection are more
                    reliable than image guessing.

                    If uncertain:
                    - use null setCode
                    - exactPrintingCertain=false
                    """),

            ChatMessageContentPart
                .CreateImagePart(
                    BinaryData.FromBytes(bytes),
                    mime,
                    ChatImageDetailLevel.High)
        ]);
    }

    // =========================================
    // CONDITION PROMPT
    // =========================================

    private static SystemChatMessage
        BuildConditionPrompt(
            decimal? eurPrice,
            decimal? usdPrice)
    {
        return new SystemChatMessage($$"""
        You are evaluating the physical
        condition of a Magic The Gathering card.

        Analyze:
        - edge wear
        - scratches
        - whitening
        - bends
        - surface damage
        - dirt
        - centering

        Estimate:
        - condition
        - confidence
        - visible issues
        - notes
        - estimated value multiplier

        Allowed conditions:
        Mint
        Near Mint
        Light Played
        Moderately Played
        Heavily Played
        Damaged

        Suggested multipliers:
        Mint = 1.1
        Near Mint = 1.0
        Light Played = 0.85
        Moderately Played = 0.7
        Heavily Played = 0.5
        Damaged = 0.25

        Current market prices:
        EUR={{eurPrice}}
        USD={{usdPrice}}

        Return ONLY valid JSON.
        """);
    }

    // =========================================
    // HELPERS
    // =========================================

    private static string CreateHash(
        byte[] bytes)
    {
        using var sha =
            SHA256.Create();

        return Convert.ToHexString(
            sha.ComputeHash(bytes));
    }

    private static string GetMimeType(
        string filePath)
    {
        var ext =
            Path.GetExtension(filePath)
                .ToLowerInvariant();

        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}