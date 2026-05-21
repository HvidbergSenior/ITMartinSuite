using System.Collections.Concurrent;
using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Ai.Services;

public sealed class OpenAiMagicCardRecognitionService
    : OpenAiServiceBase,
      IMagicCardRecognitionService
{
    private static readonly
        ConcurrentDictionary<string, MagicCardAnalysisResult>
        Cache = new();

    public OpenAiMagicCardRecognitionService(
        IConfiguration configuration)
        : base(configuration)
    {
    }

    public async Task<MagicCardAnalysisResult?>
        AnalyzeAsync(
            string filePath,
            CardDetectionResult detection)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

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
                    BuildSystemPrompt(),

                    BuildUserPrompt(
                        bytes,
                        mime,
                        detection)
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
                    options);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

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

            if (result is null)
            {
                return null;
            }

            Cache[cacheKey] =
                result;

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static SystemChatMessage
        BuildSystemPrompt()
    {
        return new SystemChatMessage("""
            You are an expert Magic The Gathering
            printing identification system.

            Return ONLY valid JSON.
            """);
    }

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
                    OCR RESULTS:

                    Name:
                    {detection.Name}

                    Set:
                    {detection.SetCode}

                    Collector:
                    {detection.CollectorNumber}
                    """),

            ChatMessageContentPart
                .CreateImagePart(
                    BinaryData.FromBytes(bytes),
                    mime,
                    ChatImageDetailLevel.High)
        ]);
    }
}