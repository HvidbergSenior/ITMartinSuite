using System.Collections.Concurrent;
using System.Text.Json;
using ITMartin.Ai.Configuration;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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
            CardDetectionResult detection,
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
            You are an expert Magic: The Gathering card analysis system.

            IMPORTANT

            Your job is NOT to determine the card printing.
            Your job is NOT to determine the card edition.
            Your job is NOT to determine the set code.

            Your job is ONLY to describe what is physically visible on the card.

            Extract:

            - Name
            - Artist
            - Collector Number
            - Copyright Year
            - Visible Set Symbol Description
            - Whether a Set Symbol is visible
            - White Border
            - Old Border
            - Mana Cost
            - Card Type
            - Power/Toughness
            - Rarity

            RULES

            - Never guess a set code.
            - Never infer a printing.
            - Never infer a set from the card name.
            - Never infer a set from artwork.
            - Never infer a set from artist.
            - Never infer a set from copyright year.
            - Never infer a collector number.
            - Only report what is visible.

            SET SYMBOL RULES

            If a symbol is visible:

            Describe the symbol in enough detail that
            software could later identify it.
            
            Examples:
            
            "Eye with central pupil"
            "Palm tree with curved trunk"
            "Five pointed star"
            "Anvil"
            "Crescent moon"
            "Dragon head facing left"
            "Cloud with lightning"
            "Shield"
            
            Do NOT map symbols to sets.

            If no symbol is visible:

            SetSymbolVisible = false
            VisibleSetSymbolDescription = null

            COLLECTOR NUMBER RULES

            Only return CollectorNumber if physically visible.

            If unreadable:

            CollectorNumber = null

            CONFIDENCE

            Confidence refers to confidence in the observations.

            RETURN JSON ONLY

            Properties:

            name
            artist
            collectorNumber
            copyrightYear
            visibleSetSymbolDescription
            setSymbolVisible
            whiteBorder
            oldBorder
            manaCost
            cardType
            powerToughness
            rarity
            confidence
            """);
    }

    private UserChatMessage BuildUserPrompt(
        byte[] bytes,
        string mime,
        CardDetectionResult detection)
    {
        return new UserChatMessage(
        [
            ChatMessageContentPart
                .CreateTextPart(
                    $$"""
                      OCR RESULTS

                      Name:
                      {{detection.Name}}

                      Set:
                      {{detection.SetCode}}

                      Collector:
                      {{detection.CollectorNumber}}

                      OCR values are hints only.

                      Use the image as the source of truth.

                      If OCR disagrees with the image,
                      ignore OCR.

                      Describe only what is physically visible.

                      IMPORTANT

                      Do not determine a set.
                      Do not determine a printing.
                      Do not determine an edition.

                      Never guess.

                      If an expansion symbol is visible:

                      - Describe the symbol.
                      - Describe its shape.
                      - Describe any distinctive details.

                      Examples:

                      "Eye with central pupil"
                      "Palm tree"
                      "Anvil"
                      "Crescent moon"
                      "Five pointed star"
                      "Dragon head"

                      If no symbol is clearly visible:

                      SetSymbolVisible = false
                      VisibleSetSymbolDescription = null

                      If a collector number is not readable:

                      CollectorNumber = null

                      Report observations only.
                      """),

            ChatMessageContentPart
                .CreateImagePart(
                    BinaryData.FromBytes(bytes),
                    mime,
                    ChatImageDetailLevel.High)
        ]);
    }
}