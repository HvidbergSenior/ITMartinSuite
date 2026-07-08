using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Ai.Services;

public sealed class ClaudeMagicCardRecognitionService
    : IMagicCardRecognitionService
{
    private const int MaxCacheSize = 500;

    private static readonly
        ConcurrentDictionary<string, MagicCardAnalysisResult>
        Cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportCardTool = new()
    {
        Name = "report_card",
        Description = "Report the identified Magic: The Gathering card details",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["identifiedName"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "The card name" }),
                ["collectorNumber"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Collector number — null if any digit is unclear" }),
                ["artist"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Artist name only, no 'Illus.' prefix" }),
                ["manaCost"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "The mana cost in MTG brace notation e.g. {3}{G} or {X}{R}{R}" }),
                ["cardType"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "The card type line" }),
                ["powerToughness"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Power/toughness e.g. 2/3" }),
                ["borderColor"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Border color: black or white" }),
                ["copyrightYear"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Copyright year printed at the bottom of the card e.g. 1995" }),
                ["hasLineUnderArtist"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "Is there anything (a rule line, extra text/element) printed directly below the artist credit line? Revised Edition has nothing there — later editions do." }),
                ["identificationConfidence"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Confidence 0.0-1.0" }),
            },
            Required = ["identificationConfidence", "borderColor", "copyrightYear"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeMagicCardRecognitionService> _logger;

    public ClaudeMagicCardRecognitionService(
        IConfiguration configuration,
        ILogger<ClaudeMagicCardRecognitionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Missing Anthropic API key");
        }

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<MagicCardAnalysisResult?> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath,
                    cancellationToken);

            const int MaxBytes = 4 * 1024 * 1024;
            if (bytes.Length > MaxBytes)
            {
                using var image = Image.Load(bytes);
                const int MaxDimension = 1600;
                if (image.Width > MaxDimension || image.Height > MaxDimension)
                {
                    var ratio = Math.Min((double)MaxDimension / image.Width, (double)MaxDimension / image.Height);
                    image.Mutate(x => x.Resize(
                        Math.Max(1, (int)(image.Width * ratio)),
                        Math.Max(1, (int)(image.Height * ratio))));
                }

                var quality = 85;
                byte[] resized;
                do
                {
                    using var ms = new MemoryStream();
                    await image.SaveAsJpegAsync(ms,
                        new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality },
                        cancellationToken);
                    resized = ms.ToArray();
                    quality -= 10;
                } while (resized.Length > MaxBytes && quality > 20);

                bytes = resized;
            }

            var cacheKey = CreateHash(bytes);

            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var mime = GetMimeType(filePath);
            var base64 = Convert.ToBase64String(bytes);

            var request = new MessageCreateParams
            {
                Model = Model.ClaudeOpus4_8,
                MaxTokens = 1024,
                System = SystemPromptText,
                Tools = [ReportCardTool],
                ToolChoice = new ToolChoiceTool { Name = "report_card" },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new TextBlockParam { Text = UserPromptText },
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource
                                {
                                    Data = base64,
                                    MediaType = mime,
                                }
                            },
                        }
                    }
                ]
            };

            var response =
                await _client.Messages.Create(request);

            ToolUseBlock? toolUse = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickToolUse(out var tu))
                {
                    toolUse = tu;
                    break;
                }
            }

            if (toolUse is null)
                return null;

            var json = JsonSerializer.Serialize(toolUse.Input);

            _logger.LogInformation(
                "Claude tool response: {Input}",
                json);

            var result =
                JsonSerializer.Deserialize<MagicCardAnalysisResult>(
                    json,
                    JsonOptions);

            if (result is null)
                return null;

            if (Cache.Count >= MaxCacheSize)
                Cache.Clear();

            Cache[cacheKey] = result;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude recognition failed");
            throw;
        }
    }

    private static string CreateHash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string GetMimeType(string filePath)
    {
        var ext =
            Path.GetExtension(filePath)
                .ToLowerInvariant();

        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    private const string SystemPromptText = """
        You are an expert Magic: The Gathering card identification system.

        GOAL

        Identify the card and extract information that can uniquely identify the printing.

        GENERAL RULES

        * A missing value is better than an incorrect value.
        * Never guess.
        * Only return information directly visible on the card.
        * If uncertain, omit the field (leave it out of the tool call).

        PRIORITY 1 — CARD IDENTIFICATION

        * identifiedName

        PRIORITY 2 — PRINTING IDENTIFICATION

        * collectorNumber

        PRIORITY 3 — PRINTING SUPPORT

        * artist

        PRIORITY 3a — MATCH SUPPORT

        * manaCost
        * cardType
        * powerToughness

        MANA COST RULES

        Always use MTG brace notation: {W} {U} {B} {R} {G} {C} {X} {0}–{20}

        Examples: "3G" → {3}{G} / "2WW" → {2}{W}{W} / "XRR" → {X}{R}{R}

        BORDER COLOR RULES

        Every Magic card has a border. Look at the physical border — the colored frame that surrounds the entire card face.

        Always report this field. Return "black" or "white" only.

        Examples:
        - Dark/near-black outer frame → "black"
        - Light/white outer frame → "white"

        COPYRIGHT YEAR RULES

        Every Magic card has a copyright line at the very bottom. It always looks like "© 1993 Wizards of the Coast, Inc." or similar.

        Always report this field. Return only the 4-digit year.

        Examples:
        - "© 1993 Wizards of the Coast, Inc." → "1993"
        - "© 1994 Wizards of the Coast, Inc." → "1994"
        - "© 1995 Wizards of the Coast, Inc." → "1995"

        Only omit copyrightYear if the bottom of the card is physically cut off in the image.

        LINE UNDER ARTIST RULES (old-frame cards only — Alpha through 4th Edition)

        Look directly below the artist credit line at the bottom of the card. Revised Edition
        has nothing there — the area below the artist name is blank. 4th Edition and later
        printings have something there (an extra line or element).

        Report hasLineUnderArtist = false if that area is blank, true if anything is there.
        This matters most when the copyright year is too small/worn to read reliably —
        it's a more robust way to tell Revised Edition apart from 4th Edition.

        REVISED EDITION SIGNATURE

        Revised Edition's two defining traits together: no visible/legible copyright year,
        AND nothing printed under the artist line. If you observe both of these missing,
        that strongly indicates Revised Edition specifically (not 4th Edition or Unlimited).

        OLD-FRAME SET GUIDE (only relevant when a card name exists in more than one of
        these sets — this only matters for disambiguation, not for cards printed once)

        - Alpha (lea): black border. UNIQUE TRAIT — rounded card corners. This is the only
          set with rounded corners; if you can see the corners, this alone confirms Alpha.
        - Beta (leb): black border, square corners. Visually near-identical to Alpha except
          corners — rely on copyright year/collector number if corners aren't visible.
        - Unlimited (2ed): white border — the first white-border set.
        - Revised (3ed): white border. No copyright year visible, no line under artist (see above).
        - 4th Edition (4ed): white border. Has copyright year "1995" and a line under artist.
        - 4th Edition Foreign Black Border (4bb): black border, but the card text is in a
          foreign language (Spanish or similar), not English — a card with English rules
          text is never 4bb.
        - 5th Edition (5ed): white border, released 1997 — noticeably later than the other
          white-border core sets. If the card looks newer/crisper than a worn 1990s card,
          consider 5th Edition.
        - Arabian Nights (arn) / Antiquities (atq): black border expansion sets from 1993-94
          with their own distinct card pools (not reprints of core-set staples) — normally
          only relevant if the card name is specific to these sets.
        - Chronicles (chr) / Renaissance (ren): 1995 reprint sets — if the physical card has
          any visible expansion symbol at all, it is NOT one of the no-symbol core sets above;
          check for a small icon near the card name before assuming no set symbol.

        COLLECTOR NUMBER RULES

        Collector Number and Power/Toughness are different things.

        Collector Number must be completely readable.

        If any digit or character is unclear, omit collectorNumber entirely.

        Never estimate missing digits.

        Never return partial collector numbers.

        ARTIST RULES

        Return only the artist name.

        Do not include: Illus. / Illustration / Illustrated by

        Example: "Illus. Richard Thomas" → "Richard Thomas"

        CARD IDENTIFICATION RULES

        Identify: card name, mana cost, card type, power/toughness.

        Visible text always has priority over memory.

        If the card name is not visible, use other visible information to identify the card.

        Never set identificationConfidence above 0.7 when the card name itself is not visible.
        """;

    private const string UserPromptText = """
        Analyze this Magic: The Gathering card image.

        Priority:

        1. Card Name
        2. Collector Number
        3. Artist
        4. Mana Cost
        5. Card Type
        6. Power/Toughness
        7. Border Color — look at the outer frame: is it black or white?
        8. Copyright Year — look at the very bottom of the card for a line like "© 1993 Wizards of the Coast, Inc."
        9. Line Under Artist — for old-frame cards, is there anything printed directly below the artist credit? Revised Edition has nothing there.

        Read only information that is directly visible.

        Collector Number is extremely important — never guess missing digits.

        Call the report_card tool with your findings.
        """;
}
