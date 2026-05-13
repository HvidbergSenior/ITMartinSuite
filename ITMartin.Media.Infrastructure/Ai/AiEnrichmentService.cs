using System.Text.Json;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class AiEnrichmentService : IAiEnrichmentService
{
    private static readonly HashSet<string> AllowedCategories =
    [
        "Travel",
        "Family",
        "Work",
        "Screenshots",
        "Documents",
        "Music",
        "Movies",
        "Games",
        "Memes",
        "Receipts",
        "Unknown"
    ];

    private readonly ChatClient _client;


    public AiEnrichmentService(
        IConfiguration configuration)
    {

        var apiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("Missing OpenAI API key");

        _client = new ChatClient(
            model: "gpt-4.1-mini",
            apiKey: apiKey);
    }

    public async Task EnrichBatchAsync(
        List<MediaFile> files,
        Func<Task>? onBatchCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var filesToProcess = files
            .Where(NeedsAi)
            .ToList();

        if (filesToProcess.Count == 0)
            return;

        const int batchSize = 5;

        foreach (var batch in filesToProcess.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchList = batch.ToList();

            try
            {
                
                // =========================
                // BATCH CLASSIFICATION
                // =========================

                var results = await ProcessBatchAsync(
                    batchList,
                    cancellationToken);

                if (results == null)
                    continue;

                foreach (var result in results)
                {
                    Console.WriteLine("=================================");
                    Console.WriteLine($"ID: {result.Id}");
                    Console.WriteLine($"Category: {result.Category}");
                    Console.WriteLine($"SubCategory: {result.SubCategory}");
                    Console.WriteLine($"Description: {result.Description}");
                    Console.WriteLine($"Confidence: {result.Confidence}");

                    var file = batchList
                        .FirstOrDefault(x => x.Id == result.Id);

                    if (file == null)
                        continue;

                    Console.WriteLine(
                        $"FILE: {file.FullPath}");

                    var category =
                        AllowedCategories.Contains(
                            result.Category)
                            ? result.Category
                            : "Unknown";

                    file.AiCategory = category;
                    file.AiSubCategory = result.SubCategory;

                    // Keep best vision description if available

                    if (string.IsNullOrWhiteSpace(
                            file.AiDescription))
                    {
                        file.AiDescription =
                            result.Description;
                    }

                    file.AiConfidence =
                        Math.Max(
                            file.AiConfidence ?? 0,
                            (float?)result.Confidence ?? 0);

                    file.AiProcessed = true;
                }

                Console.WriteLine(
                    $"Processed AI batch: {batchList.Count} files");

                await (onBatchCompleted?.Invoke()
                       ?? Task.CompletedTask);

                await Task.Delay(
                    2000,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"AI BATCH ERROR: {ex}");

                await Task.Delay(
                    10000,
                    cancellationToken);
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
                var response =
                    await _client.CompleteChatAsync(
                    [
                        new SystemChatMessage("""
                        You are a file classification AI.

                        Return ONLY valid JSON.
                        Return ONLY a JSON array.

                        Never skip files.
                        Never return markdown.
                        Never explain anything.
                        """),

                        new UserChatMessage(prompt)
                    ],
                    new ChatCompletionOptions
                    {
                        Temperature = 0
                    },
                    cancellationToken);

                var text =
                    response.Value.Content
                        .FirstOrDefault()?.Text;

                Console.WriteLine(
                    $"RAW AI RESPONSE: {text}");

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                var results =
                    JsonSerializer.Deserialize<
                        List<AiBatchResult>>(
                        text,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                return results;
            }
            catch (JsonException ex)
            {
                Console.WriteLine(
                    $"JSON PARSE ERROR (attempt {attempt}): {ex}");

                if (attempt == 2)
                    throw;

                await Task.Delay(
                    2000,
                    cancellationToken);
            }
        }

        return null;
    }

    public async Task<string> TestAsync()
    {
        var response =
            await _client.CompleteChatAsync(
            [
                new UserChatMessage("Say hello")
            ]);

        return response.Value.Content
            .FirstOrDefault()?.Text ?? "No response";
    }

    private static bool NeedsAi(
        MediaFile file)
    {
        return file.RequiresReview;
    }

    private static string BuildBatchPrompt(
        List<MediaFile> files)
    {
        var items = files.Select(x => new
        {
            x.Id,

            FileName = Path.GetFileName(
                x.NormalizedPath ??
                x.FullPath),

            MediaType = x.Type.ToString(),

            OCR =
                string.IsNullOrWhiteSpace(x.OcrText)
                    ? null
                    : x.OcrText.Length > 3000
                        ? x.OcrText[..3000]
                        : x.OcrText,

            ImageDescription =
                string.IsNullOrWhiteSpace(x.AiDescription)
                    ? null
                    : x.AiDescription,

            ImageTags =
                x.AiTags?.Any() == true
                    ? x.AiTags
                    : null,

            Width = x.Width,
            Height = x.Height,
            Year = x.Year
        });

        var json =
            JsonSerializer.Serialize(
                items,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

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