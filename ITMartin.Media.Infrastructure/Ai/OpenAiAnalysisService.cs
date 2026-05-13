using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class OpenAiAnalysisService
    : IAiAnalysisService
{
    private readonly ChatClient _client;

    // simple in-memory cache
    private static readonly Dictionary<string, object>
        Cache = new();

    public OpenAiAnalysisService(
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

    public async Task<AiAnalysisResult>
        AnalyzeImageAsync(
            string filePath)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

            var cacheKey =
                CreateHash(bytes);

            if (Cache.TryGetValue(
                    $"image-{cacheKey}",
                    out var cached))
            {
                return (AiAnalysisResult)cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    new SystemChatMessage("""
                    You analyze images.

                    Return ONLY valid JSON.

                    Example:

                    {
                      "description": "A child skiing in snow",
                      "tags": ["skiing", "snow", "child"],
                      "confidence": 0.95
                    }
                    """),

                    new UserChatMessage(
                    [
                        ChatMessageContentPart.CreateTextPart(
                            "Analyze this image"),

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
                $"OPENAI RESPONSE: {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                return Empty();
            }

            var result =
                JsonSerializer.Deserialize<
                    AiAnalysisResult>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                return Empty();
            }

            Cache[$"image-{cacheKey}"] =
                result;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"OPENAI ERROR: {ex}");

            return Empty();
        }
    }

    public async Task<MagicCardAnalysisResult?>
        AnalyzeMagicCardAsync(
            string filePath)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

            var cacheKey =
                CreateHash(bytes);

            if (Cache.TryGetValue(
                    $"mtg-{cacheKey}",
                    out var cached))
            {
                return
                    (MagicCardAnalysisResult)cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    // REPLACE ONLY YOUR SYSTEM PROMPT
// inside AnalyzeMagicCardAsync

                    new SystemChatMessage("""
                                          You are an expert Magic The Gathering
                                          printing identification system.

                                          Your job is to identify the EXACT printing/version
                                          of the card shown.

                                          IMPORTANT:
                                          Many old MTG cards DO NOT contain set symbols.

                                          If:
                                          - set symbol is missing
                                          - collector number is unreadable
                                          - copyright line unclear

                                          then DO NOT confidently guess the set.

                                          Use null if uncertain.

                                          Priority order:
                                          1. Collector number
                                          2. Set symbol
                                          3. Border color
                                          4. Copyright line
                                          5. Artist
                                          6. Card frame/border
                                          7. Mana cost
                                          8. Layout/template era
                                          9. Rarity color

                                          Very important:
                                          - white border is NOT Alpha/Beta
                                          - Alpha/Beta are black bordered
                                          - old cards often have ambiguous printings

                                          Determine:
                                          - if border is white
                                          - if exact printing is certain

                                          Return ONLY valid JSON.

                                          Required JSON format:

                                          {
                                            "cardName": "string",
                                            "setCode": "string or null",
                                            "collectorNumber": "string or null",
                                            "artist": "string or null",
                                            "manaCost": "string or null",
                                            "cardType": "string or null",
                                            "rarity": "string or null",
                                            "copyright": "string or null",
                                            "isOldBorder": true,
                                            "isWhiteBorder": true,
                                            "exactPrintingCertain": false,
                                            "releaseEra": "string",
                                            "confidence": 0.85
                                          }
                                          """),

                    new UserChatMessage(
                    [
                        ChatMessageContentPart.CreateTextPart(
                            """
                            Analyze this Magic card.
                            Identify the exact printing.
                            Focus heavily on:
                            - collector number
                            - set symbol
                            - artist
                            - copyright
                            """
                        ),

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
                $"MAGIC AI RESPONSE: {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

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

            Cache[$"mtg-{cacheKey}"] =
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

            if (Cache.TryGetValue(
                    $"condition-{cacheKey}",
                    out var cached))
            {
                return
                    (CardConditionResult)cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    new SystemChatMessage($$"""
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

                    Example:

                    {
                      "condition":"Light Played",
                      "confidence":0.88,
                      "issues":["edge wear","small scratches"],
                      "notes":"Visible whitening on corners",
                      "estimatedValueMultiplier":0.85
                    }
                    """),

                    new UserChatMessage(
                    [
                        ChatMessageContentPart.CreateTextPart(
                            "Analyze this card condition"),

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

            Cache[$"condition-{cacheKey}"] =
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

    private static string CreateHash(
        byte[] bytes)
    {
        using var sha =
            SHA256.Create();

        var hash =
            sha.ComputeHash(bytes);

        return Convert.ToHexString(hash);
    }

    private static string GetMimeType(
        string filePath)
    {
        var ext =
            Path.GetExtension(filePath)
                .ToLower();

        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    private static AiAnalysisResult Empty()
    {
        return new AiAnalysisResult
        {
            Description = "Unknown",
            Tags = [],
            Confidence = 0
        };
    }
}