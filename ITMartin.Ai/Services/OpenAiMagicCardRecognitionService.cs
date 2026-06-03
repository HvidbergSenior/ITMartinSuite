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

    private readonly MagicSetSymbolOptions
        _setOptions;

    public OpenAiMagicCardRecognitionService(
        IConfiguration configuration,
        IOptions<MagicSetSymbolOptions> setOptions)
        : base(configuration)
    {
        _setOptions =
            setOptions.Value;
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

    private SystemChatMessage
        BuildSystemPrompt()
    {
        var supportedSets =
            string.Join(
                Environment.NewLine +
                Environment.NewLine,
                _setOptions.Symbols.Select(x =>
                    $"{x.SetCode} = {x.SetName}{Environment.NewLine}- {x.Description}"));

        var supportedSetCodes =
            string.Join(
                ", ",
                _setOptions.Symbols.Select(x =>
                    x.SetCode));

        return new SystemChatMessage(
            $"""
            You are an expert Magic: The Gathering card identification system.

            Set Symbol Rules

            The card may belong to one of the following supported sets:

            {supportedSets}

            Instructions:

            - Inspect the visible set symbol.
            - Compare it against the supported symbols.
            - Primary evidence is the visible set symbol.
            - Secondary evidence may include card frame, OCR text, artist, copyright line and other visible card details.
            - Never allow secondary evidence to override a clearly visible symbol.
            - If a strong match exists, populate SetCode.
            - If no strong match exists, leave SetCode empty.
            - If SetCode is empty, populate SetSymbolDescription with a detailed description.
            - Never invent a set code.
            - Never guess.
            - SetCode must be one of the supported set codes listed above.
            - If no supported symbol matches, SetCode must be an empty string.

            Supported Set Codes:

            {supportedSetCodes}

            Collector Number Rules

            - Only return CollectorNumber if it is directly visible and readable.
            - Never infer, estimate, guess, derive or look up CollectorNumber.
            - CollectorNumber must come only from text physically visible in the image.
            - If uncertain, return null.

            Return ONLY valid JSON matching this schema:
            Return ONLY valid JSON with these properties:
            
            name
            artist
            setCode
            setSymbolDescription
            collectorNumber
            oldBorder
            whiteBorder
            powerToughness
            manaCost
            cardType
            rarity
            confidence
            exactPrintingCertain
            
            Return JSON only.
            """);
    }

    private UserChatMessage
        BuildUserPrompt(
            byte[] bytes,
            string mime,
            CardDetectionResult detection)
    {
        var supportedSets =
            string.Join(
                Environment.NewLine,
                _setOptions.Symbols.Select(x =>
                    $"{x.SetCode} = {x.SetName}"));

        return new UserChatMessage(
        [
            ChatMessageContentPart
                .CreateTextPart(
                    $"""
                    OCR RESULTS

                    Name:
                    {detection.Name}

                    Set:
                    {detection.SetCode}

                    Collector:
                    {detection.CollectorNumber}

                    OCR SET SYMBOL CANDIDATE

                    {detection.SetCode}

                    If the OCR candidate matches the visible symbol,
                    prefer that value.

                    If the visible symbol clearly disagrees,
                    override it.

                    IMPORTANT

                    Carefully inspect the visible set symbol.

                    Supported symbols:

                    {supportedSets}

                    If none match:

                    - Leave SetCode empty.
                    - Populate SetSymbolDescription.
                    - Do not guess.

                    Remember:

                    Primary evidence is the visible set symbol.
                    OCR is only a secondary hint.
                    """),

            ChatMessageContentPart
                .CreateImagePart(
                    BinaryData.FromBytes(bytes),
                    mime,
                    ChatImageDetailLevel.High)
        ]);
    }
}