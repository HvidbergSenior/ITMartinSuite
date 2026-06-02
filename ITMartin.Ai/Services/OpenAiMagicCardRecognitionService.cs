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
            CardDetectionResult detection, CancellationToken cancellationToken)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath, cancellationToken);

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
                    options, cancellationToken);

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
        catch (Exception ex)
        {
            Console.WriteLine(
                $"OpenAI recognition failed: {ex}");

            throw;
        }
    }

    private static SystemChatMessage
        BuildSystemPrompt()
    {
        return new SystemChatMessage("""
                                     You are an expert Magic: The Gathering card identification system.

                                     Set Symbol Rules:
                                     
                                     - Carefully inspect the set symbol.
                                     - Determine the symbol shape, color, and visual characteristics.
                                     - If the symbol is recognizable, return the official MTG set code.
                                     - If the symbol is not recognizable, describe it in detail.
                                     - The description should contain enough detail for a human to identify the set.
                                     - Never invent a set code.
                                     - Never leave SetSymbolDescription empty when a set symbol is visible.
                                     
                                     Collector Number Rules:
                                     
                                     - Only return CollectorNumber if it is directly visible and readable in the image.
                                     - Never infer, estimate, guess, derive, or look up CollectorNumber.
                                     - Do not use prior knowledge of Magic: The Gathering cards.
                                     - Do not use card name, set code, artist, rarity, mana cost, card type, or artwork to determine CollectorNumber.
                                     - CollectorNumber must come only from text physically visible in the image.
                                     - If there is any uncertainty, return null.
                                     
                                     Return ONLY valid JSON matching this schema:

                                     {
                                       "name": "",
                                       "artist": "",
                                       "setCode": "",
                                       "setSymbolDescription": "",
                                       "collectorNumber": null,
                                       "oldBorder": false,
                                       "whiteBorder": false,
                                       "powerToughness": "",
                                       "manaCost": "",
                                       "cardType": "",
                                       "rarity": "",
                                       "confidence": 0,
                                       "exactPrintingCertain": false
                                     }

                                     Return JSON only.
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

                         IMPORTANT:

                         Carefully inspect the set symbol.

                         If you recognize the symbol,
                         return the official MTG set code.

                         If you do not recognize the symbol,
                         return a detailed description in
                         SetSymbolDescription.
                         """),

                ChatMessageContentPart
                    .CreateImagePart(
                        BinaryData.FromBytes(bytes),
                        mime,
                        ChatImageDetailLevel.High)
            ]);
        }
    }
