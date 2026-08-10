using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class AiEnrichmentService : IAiEnrichmentService
{
    private static readonly HashSet<string> AllowedCategories =
    [
        "Travel", "Family", "Work", "Screenshots", "Documents",
        "Music", "Movies", "Games", "Memes", "Receipts", "Unknown"
    ];

    private readonly AnthropicClient _client;
    private readonly ILogger<AiEnrichmentService> _logger;

    public AiEnrichmentService(
        IConfiguration configuration,
        ILogger<AiEnrichmentService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task EnrichBatchAsync(
        List<MediaFile> files,
        Func<Task>? onBatchCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var filesToProcess = files.Where(NeedsAi).ToList();

        if (filesToProcess.Count == 0)
            return;

        const int batchSize = 5;

        foreach (var batch in filesToProcess.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchList = batch.ToList();

            try
            {
                var results = await ProcessBatchAsync(batchList, cancellationToken);

                if (results == null)
                    continue;

                foreach (var result in results)
                {
                    _logger.LogDebug(
                        "AI result — id:{Id} category:{Category} sub:{Sub} confidence:{Confidence}",
                        result.Id, result.Category, result.SubCategory, result.Confidence);

                    var file = batchList.FirstOrDefault(x => x.Id == result.Id);

                    if (file == null)
                        continue;

                    var category = AllowedCategories.Contains(result.Category)
                        ? result.Category
                        : "Unknown";

                    file.AiCategory = category;
                    file.AiSubCategory = result.SubCategory;

                    if (string.IsNullOrWhiteSpace(file.AiDescription))
                        file.AiDescription = result.Description;

                    file.AiConfidence = Math.Max(
                        file.AiConfidence ?? 0,
                        (float?)result.Confidence ?? 0);

                    file.AiProcessed = true;
                }

                _logger.LogInformation("Processed AI batch: {Count} files", batchList.Count);

                await (onBatchCompleted?.Invoke() ?? Task.CompletedTask);

                await Task.Delay(2000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI batch error");
                await Task.Delay(10000, cancellationToken);
            }
        }
    }

    private async Task<List<AiBatchResult>?> ProcessBatchAsync(
        List<MediaFile> batch,
        CancellationToken cancellationToken)
    {
        var prompt = BuildBatchPrompt(batch);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var request = new MessageCreateParams
                {
                    Model = Model.ClaudeHaiku4_5,
                    MaxTokens = 2048,
                    System = """
                        You are a file classification AI.
                        Return ONLY valid JSON.
                        Return ONLY a JSON array.
                        Never skip files.
                        Never return markdown.
                        Never explain anything.
                        """,
                    Messages =
                    [
                        new() { Role = Role.User, Content = prompt }
                    ]
                };

                var response = await _client.Messages.Create(request);

                string? text = null;
                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out var tb))
                    {
                        text = tb.Text;
                        break;
                    }
                }

                _logger.LogDebug("Raw AI response: {Text}", text);

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                return JsonSerializer.Deserialize<List<AiBatchResult>>(
                    text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON parse error (attempt {Attempt})", attempt);

                if (attempt == 2)
                    throw;

                await Task.Delay(2000, cancellationToken);
            }
        }

        return null;
    }

    public async Task<List<UnhandledClassificationItem>> ClassifyUnhandledBatchAsync(
        List<(Guid Id, string RelativePath)> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return [];

        var prompt = BuildUnhandledPrompt(items);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var request = new MessageCreateParams
                {
                    Model = Model.ClaudeHaiku4_5,
                    MaxTokens = 8192,
                    System = """
                        You are a file classification AI.
                        Return ONLY valid JSON.
                        Return ONLY a JSON array.
                        Never skip files.
                        Never return markdown.
                        Never explain anything.
                        """,
                    Messages =
                    [
                        new() { Role = Role.User, Content = prompt }
                    ]
                };

                var response = await _client.Messages.Create(request);

                string? text = null;
                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out var tb))
                    {
                        text = tb.Text;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                    return [];

                return JsonSerializer.Deserialize<List<UnhandledClassificationItem>>(
                    StripCodeFence(text),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON parse error classifying Unhandled batch (attempt {Attempt})", attempt);

                if (attempt == 2)
                    return [];

                await Task.Delay(2000, cancellationToken);
            }
        }

        return [];
    }

    // Haiku's system prompt says "never return markdown", but it doesn't
    // always listen - a leading/trailing ```json fence is common enough
    // that every JSON-response caller here should tolerate it rather than
    // hard-fail the whole batch on a JsonException.
    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```"))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return trimmed;

        var withoutOpenFence = trimmed[(firstNewline + 1)..];

        var closingFenceIndex = withoutOpenFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpenFence[..closingFenceIndex].Trim()
            : withoutOpenFence.Trim();
    }

    private static string BuildUnhandledPrompt(List<(Guid Id, string RelativePath)> items)
    {
        var json = JsonSerializer.Serialize(
            items.Select(x => new { x.Id, x.RelativePath }),
            new JsonSerializerOptions { WriteIndented = true });

        return $$"""
You are classifying files that FileSorter could not recognize by extension -
these came from a raw folder/backup dump (app caches, browser favorites,
random system files, but occasionally a real photo/document with a wrong
or missing extension).

Judge ONLY from the filename and path - no file content is provided.

For each file, return a verdict:
- "Images" / "Videos" / "Documents" / "Audio" - ONLY if the filename or path
  strongly implies real personal content of that type (e.g. a path containing
  "\Documents\..." with a document-like name, or a filename that's clearly a
  photo despite an odd extension).
- "DeleteCandidate" - the path/filename clearly indicates application cache,
  browser data, OS/system files, installers, or other non-personal junk
  (e.g. AppData, Spotify cache, Favorites, .exe, .dll, .ini, Thumbs.db).
- "KeepUnhandled" - genuinely unclear, could be personal content worth a
  human's eyes - when in doubt, use this, not DeleteCandidate.

Return format example:
[
  {"id": "00000000-0000-0000-0000-000000000000", "verdict": "DeleteCandidate", "confidence": 0.9}
]

Files:
{{json}}
""";
    }

    public async Task<string> TestAsync()
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 64,
            Messages = [new() { Role = Role.User, Content = "Say hello" }]
        };

        var response = await _client.Messages.Create(request);

        string? text = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb))
            {
                text = tb.Text;
                break;
            }
        }

        return text ?? "No response";
    }

    private static bool NeedsAi(MediaFile file) => file.RequiresReview;

    private static string BuildBatchPrompt(List<MediaFile> files)
    {
        var items = files.Select(x => new
        {
            x.Id,
            FileName = Path.GetFileName(x.NormalizedPath ?? x.FullPath),
            MediaType = x.Type.ToString(),
            OCR = string.IsNullOrWhiteSpace(x.OcrText)
                ? null
                : x.OcrText.Length > 3000 ? x.OcrText[..3000] : x.OcrText,
            ImageDescription = string.IsNullOrWhiteSpace(x.AiDescription) ? null : x.AiDescription,
            ImageTags = x.AiTags?.Any() == true ? x.AiTags : null,
            Width = x.Width,
            Height = x.Height,
            Year = x.Year
        });

        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });

        return $$"""
You are an intelligent media classification AI.

Your task:
Classify media files into categories using ALL available context.

You MUST return:
- one result per file
- valid JSON only
- a JSON array only

Never:
- skip files
- explain reasoning
- return markdown
- return extra text

Use ALL available information:

1. OCR text
2. Vision AI descriptions
3. Vision AI tags
4. Filename
5. Media type
6. Metadata

Priority rules:

- VisionDescription is strongest for photos/videos.
- OCR is strongest for documents/screenshots/receipts.
- Filename is weakest and should only support classification.
- Use Unknown if confidence is low.

Confidence:
- Must be between 0 and 1.
- Use high confidence only when evidence is strong.

Allowed categories ONLY:

Travel
Family
Work
Screenshots
Documents
Music
Movies
Games
Memes
Receipts
Unknown

Category guidance:

Travel:
Vacations, landmarks, hotels, beaches,
mountains, tourism, airports, travel memories.

Family:
People, children, birthdays, pets,
family gatherings, personal life moments.

Work:
Business files, office screenshots,
work chats, presentations, spreadsheets.

Screenshots:
Desktop captures, mobile screenshots,
UI captures, websites, chat screenshots,
software screenshots.

Documents:
Scanned papers, PDFs, contracts,
letters, forms, text-heavy images.

Music:
Music files, album art, concerts,
audio-related content.

Movies:
Movies, TV shows, cinematic media,
video entertainment.

Games:
Gameplay, gaming screenshots,
game menus, gaming media.

Memes:
Funny edited images, jokes,
reaction memes, ironic screenshots,
internet humor.

Receipts:
Receipts, invoices, payment confirmations,
shopping transactions, bills.

Unknown:
Use when classification is uncertain.

SubCategory rules:

- Keep short.
- Use locations, game names,
movie names, event names,
or document types when obvious.

Examples:
- Spain
- Minecraft
- Disney
- Invoice
- Steam
- Airport

Return format example:

[
  {
    "id": "00000000-0000-0000-0000-000000000000",
    "category": "Travel",
    "subCategory": "Spain",
    "description": "Vacation photos from Spain",
    "confidence": 0.96
  }
]

Files:
{{json}}
""";
    }
}
