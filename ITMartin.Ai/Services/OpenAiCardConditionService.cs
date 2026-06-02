using System.Collections.Concurrent;
using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Ai.Services;

public sealed class OpenAiCardConditionService
    : OpenAiServiceBase,
      ICardConditionAnalysisService
{
    private static readonly
        ConcurrentDictionary<string, CardConditionResult>
        Cache = new();

    public OpenAiCardConditionService(
        IConfiguration configuration)
        : base(configuration)
    {
    }

    public async Task<CardConditionResult?>
        AnalyzeAsync(
            string filePath,
            decimal? eurPrice,
            decimal? usdPrice,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath,
                    cancellationToken);

            var cacheKey =
                CreateHash(bytes);

            if (Cache.TryGetValue(
                    cacheKey,
                    out var cached))
            {
                return cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    BuildPrompt(
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
                await Client.CompleteChatAsync(
                    messages,
                    options,
                    cancellationToken);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

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

            if (result is null)
            {
                return null;
            }

            result.AdjustedEurValue =
                eurPrice *
                result.EstimatedValueMultiplier;

            result.AdjustedUsdValue =
                usdPrice *
                result.EstimatedValueMultiplier;

            Cache[cacheKey] =
                result;

            return result;
        }
        catch (Exception)
        {
            // TODO: Log exception

            return null;
        }
    }

    private static SystemChatMessage
        BuildPrompt(
            decimal? eurPrice,
            decimal? usdPrice)
    {
        return new SystemChatMessage($"""
            Analyze the physical condition of this Magic: The Gathering card.

            EUR={eurPrice}
            USD={usdPrice}

            Estimate:
            - ConditionGrade
            - EstimatedValueMultiplier
            - Confidence
            - SurfaceWear
            - EdgeWear
            - CornerWear
            - Creases
            - Stains
            - Notes

            Return ONLY valid JSON.
            """);
    }
}