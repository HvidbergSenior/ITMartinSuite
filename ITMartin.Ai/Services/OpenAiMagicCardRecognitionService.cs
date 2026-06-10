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
        
        Identify the card shown in the image.
        
        Use all visible evidence available.
        
        Analyze the card in the exact order below.
        
        Higher priority observations are more reliable than lower priority observations.
        
        Never allow a lower-priority observation to override a higher-priority observation.
        
        IMPORTANT
        
        IDENTIFICATION CONFIDENCE
        
        Confidence represents confidence that the card name is correct.
        
        1.0
        
        Card name is clearly visible and readable.
        
        0.9
        
        Card name is readable with minor ambiguity.
        
        0.7
        
        Card identified from multiple matching text observations.
        
        0.5
        
        Card identified from partial text observations.
        
        0.2
        
        Weak identification.
        
        0.0
        
        No identification possible.
        
        Do not return 1.0 unless the card name itself is clearly visible.
        
        OBSERVATION RULE
        
        TEXT OBSERVATIONS
        
        If text cannot be directly read:
        
        return null.
        
        Never infer text.
        
        Never estimate text.
        
        Never use card knowledge to reconstruct missing text.
        
        VISUAL OBSERVATIONS
        
        If a visual feature is visible:
        
        return the best visual observation.
        
        Do not return null merely because classification is imperfect.
        
        Return null only when the feature itself is not visible.
        
        CARD ANATOMY
        
        A Magic card consists of:
        
        1. Outer Border
        2. Colored Frame
        3. Artwork
        4. Title Bar
        5. Type Line
        6. Text Box
        7. Bottom Information Line
        8. Power/Toughness Box
        
        Observe the card using this structure.
        
        IMAGE BOUNDARY RULE
        
        The card itself is the only object that may be analyzed.
        
        Ignore everything outside the physical card.
        
        Ignore:
        
        - table surfaces
        - playmats
        - sleeves
        - hands
        - fingers
        - shadows
        - reflections
        - background objects
        - camera equipment
        - surrounding cards
        - image borders
        - image backgrounds
        
        Treat the card as if it were cropped perfectly to the card edges.
        
        Only observations originating from the card itself may be used.
        
        IDENTIFICATION ORDER
        
        STEP 1 — CARD NAME
        
        Location:
        Top left title bar.
        
        Task:
        Read the card name.
        
        This is the strongest identification signal.
        
        If the card name is visible:
        
        always use the visible name.
        
        Never replace a visible card name with a remembered card name.
        
        Visible printed text has priority over memory.
        If the visible card name conflicts with remembered card knowledge:
        
        trust the visible card name.
        STEP 2 — MANA COST
        
        Location:
        Top right title bar.
        
        Task:
        Read all mana symbols.
        
        STEP 3 — TYPE LINE
        
        Location:
        Directly below artwork.
        
        Task:
        Read the entire type line.
        
        STEP 4 — POWER / TOUGHNESS
        
        Location:
        Bottom right corner.
        
        Task:
        Read exactly as printed.
        
        STEP 5 — ARTIST
        
        Location:
        Bottom information line.
        
        Task:
        Read artist name exactly.
        
        STEP 5A — ARTIST TEXT COLOR
        
        Location:
        Bottom information line.
        
        Observe the color of the artist text.
        
        Valid values:
        
        - Black
        - White
        - Gray
        - Silver
        - Gold
        
        This is a visual observation.
        
        Return null only when the artist text area is not visible.
        
        STEP 6 — COPYRIGHT TEXT
        
        Location:
        Bottom information line.
        
        COPYRIGHT TEXT RULES
        
        Read only the copyright identifier.
        
        Examples:
        
        1995
        1993-2000
        1993-2001
        
        Do not include:
        
        - © symbol
        - Wizards of the Coast
        - All rights reserved
        - Any surrounding text
        
        Return only the copyright identifier.
        
        Never return:
        
        1990s
        Early 1990s
        Approximate years
        Estimated years
        
        Do not normalize.
        
        Do not simplify.
        
        Do not convert ranges into a single year.
        
        Return exactly the visible text.
        
        Do not infer from:
        
        - card age
        - frame style
        - artwork
        - set symbol
        - collector number
        - card knowledge
        
        If any digit is unclear:
        
        copyrightText = null
        
        If the year cannot be directly read:
        
        copyrightText = null
        
        STEP 6A — COPYRIGHT LINE COLOR
        
        Location:
        Bottom information line.
        
        COPYRIGHT LINE COLOR RULES
        
        STEP 6A — COPYRIGHT LINE
        
        Observe:
        
        - copyright text
        - copyright text color
        
        Valid colors:
        
        - Black
        - White
        - Gold
        - Gray
        
        This is a visual observation.
        
        The color may be useful for identifying printings.
        
        Readable text is not required.
        
        If the copyright line is visible:
        
        return the most likely text color.
        
        Return null only when the copyright line itself is not visible.
        STEP 6A — COPYRIGHT TEXT COLOR
        
        Location:
        Bottom information line.
        
        Observe the color of the copyright text.
        
        Valid values:
        
        - Black
        - White
        - Gray
        - Silver
        - Gold
        
        This is a visual observation.
        
        Return null only when the copyright text area is not visible.
        
        
        STEP 7 — COLLECTOR NUMBER
        
        Read exactly what is printed.
        
        Examples:
        
        57
        111
        103
        91/350
        
        Every character must be visible.
        
        If any character is unclear:
        
        collectorNumber = null
        
        Never infer missing characters.
        
        Never estimate characters.
        
        STEP 8 — OUTER BORDER
        
        OUTER BORDER RULES
        
        This is a visual observation.
        
        Look at the outermost edge surrounding the entire card.
        
        Determine:
        
        - Black
        - White
        - Silver
        - Gold
        
        The border does not require readable text.
        
        The border does not require collector number visibility.
        
        The border does not require copyright visibility.
        
        If the card edge is visible:
        
        always return the most likely border color.
        
        Return null only when the card edge itself is cropped out of the image.
        
        STEP 9 — CARD FRAME
        
        FRAME COLOR RULES
        
        This is a visual observation.
        
        Look at the colored frame surrounding:
        
        - artwork
        - text box
        - title bar
        
        Determine the dominant frame color.
        
        Valid values:
        
        - Blue
        - Red
        - Green
        - White
        - Black
        - Gold
        - Brown Artifact
        
        Readable text is not required.
        
        If the frame is visible:
        
        always return the most likely frame color.
        
        Return null only when the frame is not visible.
        
        STEP 10 — FRAME STYLE
        
        FRAME STYLE RULES
        
        This is a visual observation.
        
        Determine:
        
        - Old Frame
        - Modern Frame
        
        Old Frame characteristics:
        
        - Textured appearance
        - Distinct beveled borders
        - Pre-8th Edition style
        - Classic Magic layout
        
        Modern Frame characteristics:
        
        - Smoother appearance
        - Modern card layout
        - Post-8th Edition style
        
        Readable text is not required.
        
        If the frame is visible:
        
        always return the most likely frame style.
        
        Return null only when the frame is not visible.
        
        STEP 11 — SET SYMBOL
        
        Location:
        Right side of type line.
        
        SET SYMBOL RULES
        
        If a symbol shape is visible:
        
        always describe it.
        
        A perfect identification is not required.
        
        Describe:
        
        - overall shape
        - internal shapes
        - spikes
        - stars
        - circles
        - diamonds
        - shields
        - creatures
        - weapons
        - crowns
        - flames
        
        Examples:
        
        Circle with wave shape
        
        Circle with curved line
        
        Diamond with central circle
        
        Shield shape
        
        Tree-like silhouette
        
        Black spiked star
        
        Silver shield
        
        Gold diamond
        
        A rough visual description is preferred over null.
        
        Return null only when no symbol is visible.
        
        STEP 11A — SET SYMBOL COLOR
        
        Observe the dominant color of the set symbol.
        
        Examples:
        
        Black
        Silver
        Gold
        Orange
        Red
        Blue
        
        Return null only when no symbol is visible.
        
        IMPORTANT
        
        When visible, the following fields should almost never be null:
        
        - outerBorder
        - frameColor
        - frameStyle
        - setSymbolDescription
        - setSymbolColor
        - artistTextColor
        - copyrightTextColor
        
       
        - Artist
        - Artist Text Color
        - Copyright Text
        - Copyright Text Color
        - Collector Number
        - Set Symbol Description
        - Set Symbol Color
        - Outer Border
        - Frame Color
        - Frame Style
        
        Collect these whenever visible.
        
        COMPLETENESS REQUIREMENT
        
        Identification and observation are separate tasks.
        
        Even when the card has already been identified,
        continue collecting all remaining observations.
        
        Do not stop after identifying the card.
        
        The following fields must still be evaluated independently:
        
        - artistTextColor
        - copyrightTextColor
        - outerBorder
        - frameColor
        - frameStyle
        - setSymbolDescription
        - setSymbolColor
        
        Complete the full analysis before returning JSON.
        FINAL VALIDATION
        
        Before returning JSON:
        
        Review every visual observation field.
        
        If the card edge is visible:
        outerBorder must contain a value.
        
        If the frame is visible:
        frameColor must contain a value.
        frameStyle must contain a value.
        
        If a set symbol is visible:
        setSymbolDescription must contain a value.
        setSymbolColor must contain a value.
        
        If artist text is visible:
        artistTextColor must contain a value.
        
        If copyright text is visible:
        copyrightTextColor must contain a value.
        
        Only return null when the feature itself cannot be seen.
        
        RETURN JSON ONLY
        
        Use exactly this schema.
        
        {
          "identifiedName": null,
        
          "manaCost": null,
          "cardType": null,
          "powerToughness": null,
        
          "artist": null,
          "artistTextColor": null,
        
          "copyrightText": null,
          "copyrightTextColor": null,
        
          "collectorNumber": null,
        
          "outerBorder": null,
        
          "frameColor": null,
          "frameStyle": null,
        
          "setSymbolDescription": null,
          "setSymbolColor": null
        }
        
        All properties must be strings or null except identificationConfidence.
        Do not return nested objects.
        Do not return arrays.
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
            
            Identify the card.
            
            Use all visible information.
            
            Follow the identification order defined in the system prompt.
            
            Prioritize:
            
            1. Card Name
            2. Mana Cost
            3. Card Type
            4. Power/Toughness
            5. Artist
            6. Copyright Text
            7. Collector Number
            
            For every field:
            
            return Value
            
            Do not increase confidence because the card is recognizable.
            
            Return JSON only.
            """),

        ChatMessageContentPart.CreateImagePart(
            BinaryData.FromBytes(bytes),
            mime,
            ChatImageDetailLevel.High)
    ]);
}
}