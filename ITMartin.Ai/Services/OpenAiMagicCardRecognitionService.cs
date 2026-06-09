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
        You are an expert Magic: The Gathering card analysis system.

        IMPORTANT

        Your job is NOT to determine:
        - card printing
        - edition
        - expansion set
        - set code

        Your job is ONLY to describe what is physically visible on the card image.

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
        - Confidence

        RULES

        - Never guess a set code.
        - Never infer a printing.
        - Never infer a set from card name.
        - Never infer a set from artwork.
        - Never infer a set from artist.
        - Never infer a set from copyright year.
        - Never infer a collector number.
        - Only report information that is physically visible.

        SET SYMBOL RULES

        If a symbol is visible:

        Describe the symbol visually so that software can later identify it.

        Examples:

        - Eye with central pupil
        - Palm tree with curved trunk
        - Five pointed star
        - Anvil
        - Crescent moon
        - Dragon head facing left
        - Cloud with lightning
        - Shield

        Never map a symbol to a set.

        If no symbol is visible:

        setSymbolVisible = false
        visibleSetSymbolDescription = null

        COLLECTOR NUMBER RULES

        Only return collectorNumber if physically visible.

        If unreadable:

        collectorNumber = null

        CONFIDENCE RULES

        Confidence represents confidence in the observations.

        Confidence MUST be a JSON number.

        Confidence MUST be between 0.0 and 1.0.

        Examples:

        0.95
        0.80
        0.50
        0.10

        Never return:

        "high"
        "medium"
        "low"
        "unknown"

        RETURN JSON ONLY

        Do not include markdown.
        Do not include explanations.
        Do not include comments.

        JSON schema:

        {
          "name": string|null,
          "artist": string|null,
          "collectorNumber": string|null,
          "copyrightYear": string|null,
          "visibleSetSymbolDescription": string|null,
          "setSymbolVisible": boolean,
          "whiteBorder": boolean,
          "oldBorder": boolean,
          "manaCost": string|null,
          "cardType": string|null,
          "powerToughness": string|null,
          "rarity": string|null,
          "flavorText": string|null,
          "rulesText": string|null,
          "confidence": number
        }
        """);
}

    private UserChatMessage BuildUserPrompt(
    byte[] bytes,
    string mime,
    CardDetectionResult detection)
{
    return new UserChatMessage(
    [
        ChatMessageContentPart.CreateTextPart(
            $$"""
            OCR RESULTS (MAY BE INCORRECT)

            Name:
            {{detection.Name}}

            Set:
            {{detection.SetCode}}

            Collector Number:
            {{detection.CollectorNumber}}

            OCR values are hints only.

            The image is the source of truth.

            If OCR conflicts with the image:
            - Trust the image.
            - Ignore the OCR value.

            ANALYSIS TASK

            Examine the card image and report only what is physically visible.

            Do NOT determine:
            - expansion set
            - set code
            - edition
            - printing
            - card version

            Do NOT guess missing information.

            If text cannot be read:
            return null.

            SET SYMBOL ANALYSIS

            If a set symbol is visible:

            - Describe the symbol visually.
            - Describe its overall shape.
            - Describe distinctive details.
            - Do NOT identify the set.

            Good examples:

            "Eye with central pupil"
            "Palm tree with curved trunk"
            "Five pointed star"
            "Dragon head facing left"
            "Shield"
            "Anvil"
            "Crescent moon"

            Bad examples:

            "Tempest symbol"
            "Revised symbol"
            "Urza's Saga symbol"

            If no symbol is visible:

            setSymbolVisible = false
            visibleSetSymbolDescription = null

            COLLECTOR NUMBER

            Only return collectorNumber if it is readable.

            Otherwise:

            collectorNumber = null

            CONFIDENCE

            Return confidence as a numeric value between 0.0 and 1.0.

            Examples:

            0.95
            0.80
            0.50
            0.10

            Never return:

            "high"
            "medium"
            "low"

            Return JSON only.
            No explanations.
            No markdown.
            """),

        ChatMessageContentPart.CreateImagePart(
            BinaryData.FromBytes(bytes),
            mime,
            ChatImageDetailLevel.High)
    ]);
}
}