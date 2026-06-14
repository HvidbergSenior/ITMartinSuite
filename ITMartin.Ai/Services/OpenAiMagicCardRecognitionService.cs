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
        """"
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
                
                Identify the card name and collect reliable edition-identifying observations.
                
                The card name will be used to search Scryfall.
                
                Scryfall may return many printings of the same card.
                
                Your primary responsibility is therefore NOT to identify the card.
                
                Your primary responsibility is to collect reliable observations that help distinguish one printing from another.
                
                Always prioritize edition-identifying observations over gameplay observations.
                
                A missing value is better than an incorrect value.
                
                Never invent information.
                
                Never estimate text.
                
                Never guess a collector number.
                
                Never guess a set symbol.
                
                Only return observations that are actually visible on the card.
                OBSERVATION PHILOSOPHY
                
                The goal is not to identify the edition.
                
                The goal is to collect observations that help eliminate Scryfall candidates.
                
                Think like a forensic observer.
                
                Collect facts.
                
                Do not draw conclusions.
                
                Do not infer a set.
                
                Do not infer an edition.
                
                Do not infer a printing.
                
                The application will perform the reasoning.
                
                Priority 1 - Strongest Printing Elimination Signals
                
                These observations can eliminate large numbers of candidate printings.
                
                - collectorNumber
                - setSymbolDescription
                - artist
                - copyrightText
                - outerBorder
                - frameStyle
                
                Priority 2 - Strong Printing Elimination Signals
                
                These observations often help eliminate candidate sets and printings.
                
                - setSymbolColor
                - frameColor
                - artistTextColor
                - copyrightTextColor
                
                Priority 3 - Card Identification Signals
                
                These observations help identify the card but usually do not distinguish printings.
                
                - identifiedName
                - manaCost
                - cardType
                - powerToughness
                
                Always prioritize observations that help eliminate printings.
                
                The card name is important, but Scryfall will already search by card name.
                
                The primary objective is to collect observations that help distinguish between different printings of the same card.
                
                
                If image quality is limited, spend effort on higher-priority observations first.
                
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
                
                CONSISTENCY RULES
                
                If artist text is not visible:
                
                artist = null
                artistTextColor = null
                
                If copyright text is not visible:
                
                copyrightText = null
                copyrightTextColor = null
                
                Do not return a text color for text that is not visible.
                
                The existence of text and the color of that text must agree.
                
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
                
                COLLECTOR NUMBER SAFETY RULES
                
                Collector Number and Power/Toughness are different things.
                
                Never use Power/Toughness as Collector Number.
                
                Power/Toughness appears:
                
                * Inside the rules text area
                * Bottom right of the card face
                * Example: 1/1, 2/2, 4/4
                
                Collector Number appears:
                
                * Near artist and copyright information
                * Along the bottom information line
                * Usually very small text
                
                If the only visible number is a Power/Toughness value:
                
                collectorNumber = null
                
                If the collector number cannot be clearly read:
                
                collectorNumber = null
                
                Never infer a collector number.
                
                Never estimate a collector number.
                
                Collector Number requires every character to be visible.
                
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
                
                If the visible number could be Power/Toughness:
                
                collectorNumber = null
                
                When uncertain between Collector Number and Power/Toughness:
                
                collectorNumber = null
                
                Never infer missing characters.
                
                Never estimate characters.
                
                STEP 8 — OUTER BORDER
                IMPORTANT
                
                Outer Border is NOT the card color.
                
                Outer Border is the thin edge around the entire card.
                
                Examples:
                
                A white card can have:
                - White frameColor
                - Black outerBorder
                
                A red card can have:
                - Red frameColor
                - Black outerBorder
                
                Determine the border independently from the card color.
                
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
                
                IMPORTANT
                
                Frame Color is the color of the card frame.
                
                Frame Color is NOT the border color.
                
                Examples:
                
                Red Scarab:
                frameColor = White
                outerBorder = Black
                
                Lightning Bolt:
                frameColor = Red
                outerBorder = Black
                
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
                
                STEP 10A — SET SYMBOL PRESENCE
                
                Determine whether any set symbol is visible to the right of the type line.
                
                Values:
                
                true
                false
                
                Examples:
                
                Visible symbol
                → hasSetSymbol = true
                
                No visible symbol
                → hasSetSymbol = false
                
                This observation is extremely valuable because it can eliminate many candidate printings.
                
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
                
                Silver shield
                
                Black spiked star
                
                Silver circle with starburst
                
                Gold diamond
                
                Black crown
                
                Red flame
                
                Blue droplet
                
                Tree silhouette
                
                Sword shape
                
                Shield with central emblem
                
                Circle with internal symbol
                
                Diamond with central circle
                
                Describe what is visible.
                
                Do not identify the set.
                
                Do not use set names.
                
                
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
                
                EDITION IDENTIFICATION EXAMPLES
                
                The following combinations are highly valuable because they help distinguish printings:
                
                - Artist + Copyright Text
                - Artist + Set Symbol Description
                - Copyright Text + Border Color
                - Set Symbol Description + Set Symbol Color
                - Frame Style + Border Color
                - Artist Text Color + Copyright Text Color
                - Collector Number + Set Symbol Description
                
                When these observations are visible, prioritize collecting them.
                
                These combinations are often more useful than:
                
                - Mana Cost
                - Card Type
                - Power/Toughness
                
                because many printings share identical gameplay characteristics.
                
                IMPORTANT
                
                When visible, these fields should normally contain an observation.
                
                If the feature itself cannot be seen, return null.
                
                Never guess.
                
               
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
                
                IMPORTANT
                
                Do not attempt to identify the edition.
                
                Do not attempt to identify the set.
                
                Do not attempt to infer the printing.
                
                Only describe what is visually observable.
                
                The application will determine the most likely printing using Scryfall and scoring.
                
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
                
                PRINTING ELIMINATION THINKING
                
                For every observation ask:
                
                "Can this eliminate candidate printings?"
                
                Examples:
                
                No set symbol visible
                → eliminates many expansion sets
                
                White border
                → eliminates black-border printings
                
                Black border
                → eliminates white-border printings
                
                Old frame
                → eliminates modern-frame printings
                
                Collector number visible
                → may identify a single printing
                
                Artist visible
                → may eliminate many printings of the same card
                
                Copyright year visible
                → may eliminate many later or earlier printings
                
                Set symbol visible
                → may eliminate many sets
                
                Prefer observations that eliminate candidates over observations that merely describe gameplay.
                
                EDITION IDENTIFICATION RULES
                
                The following observations are extremely valuable because they help distinguish different printings of the same card:
                
                - Artist
                - Copyright Text
                - Artist Text Color
                - Copyright Text Color
                - Collector Number
                - Set Symbol Description
                - Set Symbol Color
                - Outer Border
                - Frame Color
                - Frame Style
                
                These observations should be collected whenever visible.
                
                Mana Cost, Card Type and Power/Toughness are often identical across many printings and are therefore lower priority.
                
                When choosing between reading Mana Cost and reading a Collector Number, prioritize the Collector Number.
                
                When choosing between reading Card Type and identifying a Set Symbol, prioritize the Set Symbol.
                
                When choosing between gameplay information and edition-identifying information, prioritize edition-identifying information.
                
                HIGH VALUE COMBINATIONS
                
                The following combinations are especially valuable for distinguishing printings:
                
                - Artist + Copyright Text
                - Artist + Artist Text Color
                - Artist + Set Symbol Description
                - Copyright Text + Copyright Text Color
                - Copyright Text + Border Color
                - Copyright Text + Frame Style
                - Set Symbol Description + Set Symbol Color
                - Set Symbol Description + Border Color
                - Frame Style + Border Color
                - Frame Style + Frame Color
                - Collector Number + Set Symbol Description
                
                When these observations are visible, prioritize collecting all parts of the combination.
                
                The value comes from collecting the observations.
                
                Do not infer any edition from them.
                
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
                  {
                "hasSetSymbol": null,
                
                "identifiedName": null,
                
                "collectorNumber": null,
                
                  "setSymbolDescription": null,
                  "setSymbolColor": null,
                
                  "artist": null,
                  "artistTextColor": null,
                
                  "copyrightText": null,
                  "copyrightTextColor": null,
                
                  "frameStyle": null,
                  "frameColor": null,
                
                  "outerBorder": null,
                
                  "manaCost": null,
                  "cardType": null,
                  "powerToughness": null
                }
                
                hasSetSymbol must be true, false or null.
                
                All other properties must be strings or null except identificationConfidence.
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
                    
                    Prioritize:
                    
                    1. Card Name
                    2. Collector Number
                    3. Set Symbol Description
                    4. Artist
                    5. Copyright Text
                    6. Frame Style
                    7. Border Color
                    8. Frame Color
                    9. Artist Text Color
                    10. Copyright Text Color
                    11. Mana Cost
                    12. Card Type
                    13. Power/Toughness
                    
                    The goal is to help distinguish between multiple Scryfall printings of the same card.
                    
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
        
        hasSetSymbol must be true, false or null.
        
        All other properties must be strings or null except identificationConfidence.
        Do not return nested objects.
        Do not return arrays.
        """");
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
            
            Prioritize:
            
            1. Card Name
            2. Collector Number
            3. Set Symbol Description
            4. Artist
            5. Copyright Text
            6. Frame Style
            7. Border Color
            8. Frame Color
            9. Artist Text Color
            10. Copyright Text Color
            11. Mana Cost
            12. Card Type
            13. Power/Toughness
            
            The goal is to help distinguish between multiple Scryfall printings of the same card.
            
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