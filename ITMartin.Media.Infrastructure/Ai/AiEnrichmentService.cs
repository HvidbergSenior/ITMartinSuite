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

    private readonly IAiAnalysisService _aiAnalysisService;

    public AiEnrichmentService(
        IConfiguration configuration,
        IAiAnalysisService aiAnalysisService)
    {
        _aiAnalysisService = aiAnalysisService;

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
                // VISION ANALYSIS
                // =========================

                foreach (var file in batchList)
                {
                    try
                    {
                        if (file.Type == MediaType.Image)
                        {
                            var aiPath =
                                file.NormalizedPath ??
                                file.FullPath;

                            var vision =
                                await _aiAnalysisService
                                    .AnalyzeImageAsync(aiPath);

                            file.AiDescription =
                                vision.Description;

                            file.AiTags =
                                vision.Tags;

                            file.AiConfidence =
                                (float?)vision.Confidence;

                            Console.WriteLine(
                                $"VISION AI: {aiPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"VISION ERROR: {ex}");
                    }
                }

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

                if (onBatchCompleted != null)
                {
                    await onBatchCompleted();
                }

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

            FileName =
                Path.GetFileName(
                    x.NormalizedPath ??
                    x.FullPath),

            OcrText =
                string.IsNullOrWhiteSpace(x.OcrText)
                    ? null
                    : x.OcrText.Length > 2000
                        ? x.OcrText[..2000]
                        : x.OcrText,

            VisionDescription =
                x.AiDescription,

            VisionTags =
                x.AiTags,

            VisionConfidence =
                x.AiConfidence
        });

        var json =
            JsonSerializer.Serialize(
                items,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        return $$"""
        Classify these files.

        You MUST return one result for every file.
        Never skip files.

        VisionDescription contains AI image analysis.
        VisionTags contains detected image contents.

        Prefer VisionDescription over filename.
        Use OCR as supporting context.

        If uncertain use "Unknown".

        Confidence must be between 0 and 1.

        Allowed categories:

        Travel:
        Vacation photos/videos, hotels, flights, countries, landmarks.

        Family:
        Personal photos/videos of people, birthdays, children, pets.

        Work:
        Work-related files, office documents, business screenshots.

        Screenshots:
        Screen captures from phones, PCs, apps, websites, chats.

        Documents:
        PDFs, scans, letters, forms, contracts.

        Music:
        Audio/music related files.

        Movies:
        Movies, TV shows, video entertainment.

        Games:
        Game screenshots, gameplay captures, gaming media.

        Memes:
        Funny images, jokes, internet memes.

        Receipts:
        Store receipts, invoices, payment confirmations.

        Unknown:
        Use when classification is uncertain.

        Return ONLY a JSON array.

        Example:

        [
          {
            "id": "00000000-0000-0000-0000-000000000000",
            "category": "Travel",
            "subCategory": "Spain",
            "description": "Vacation photos from Spain",
            "confidence": 0.92
          }
        ]

        Files:
        {{json}}
        """;
    }
}