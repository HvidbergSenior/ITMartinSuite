using System.Collections.Concurrent;
using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
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
            CancellationToken cancellationToken)
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
                    BuildSystemPrompt(),

                    BuildUserPrompt(
                        bytes,
                        mime)
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
            Console.WriteLine("=== AI RESPONSE ===");
            Console.WriteLine(text);
            Console.WriteLine("===================");
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

   private SystemChatMessage BuildSystemPrompt()
{
    return new SystemChatMessage(
                """
                You are an expert Magic: The Gathering card identification system.
                
                GOAL
                
                Identify the card and extract information that can uniquely identify the printing.
                
                GENERAL RULES
                
                * A missing value is better than an incorrect value.
                * Never guess.
                * Only return information directly visible on the card.
                * If uncertain, return null.
                
                PRIORITY 1 — CARD IDENTIFICATION
                
                * identifiedName
                
                PRIORITY 2 — PRINTING IDENTIFICATION
                
                * collectorNumber
                * artist
                
                PRIORITY 3 — MATCH SUPPORT
                
                * manaCost
                * cardType
                * powerToughness
                
                COLLECTOR NUMBER RULES
                
                Collector Number and Power/Toughness are different things.
                
                If any digit is unclear:
                
                collectorNumber = null
                
                Never estimate missing digits.
                
                ARTIST RULES
                
                Read exactly what is printed.
                
                If not readable:
                
                artist = null
                
                CARD IDENTIFICATION RULES
                
                Identify:
                
                * card name
                * mana cost
                * card type
                * power/toughness
                
                Visible text always has priority over memory.
                
                If the card name is not visible:
                
                Use other visible information to identify the card.
                
                Never increase confidence above 0.7 when the card name itself is not visible.
                
                RETURN JSON ONLY
                
                {
                  "identifiedName": null,
                  "collectorNumber": null,
                  "artist": null,
                  "manaCost": null,
                  "cardType": null,
                  "powerToughness": null,
                  "identificationConfidence": 0.0
                }
                
                """);
        }

    private UserChatMessage BuildUserPrompt(
    byte[] bytes,
    string mime)
{
    return new UserChatMessage(
    [
        ChatMessageContentPart.CreateTextPart(
            $$"""
            Analyze this Magic: The Gathering card image.
            
            Priority:
            
            1. Card Name
            2. Collector Number
            3. Artist
            4. Mana Cost
            5. Card Type
            6. Power/Toughness
            
            Read only information that is directly visible.
            
            Collector Number is extremely important.
            
            Never guess missing digits.
            
            Return JSON only.
            """),

        ChatMessageContentPart.CreateImagePart(
            BinaryData.FromBytes(bytes),
            mime,
            ChatImageDetailLevel.High)
    ]);
}
}