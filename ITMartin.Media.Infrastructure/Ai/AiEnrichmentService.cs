using System.Text.Json;
using ITMartin.Media.Entities;
using ITMartin.Media.Interfaces;
using ITMartin.Media.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class AiEnrichmentService : IAiEnrichmentService
{
    private readonly ChatClient _client;

    public AiEnrichmentService(IConfiguration configuration)
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
        Func<Task>? onBatchCompleted = null)
    {
        var filesToProcess = files
            .Where(NeedsAi)
            .ToList();

        if (filesToProcess.Count == 0)
            return;

        const int batchSize = 10;

        foreach (var batch in filesToProcess.Chunk(batchSize))
        {
            try
            {
                var batchList = batch.ToList();

                var prompt = BuildBatchPrompt(batchList);

                var response = await _client.CompleteChatAsync(
                [
                    new SystemChatMessage("""
                    You are a file classification AI.

                    Return ONLY valid JSON.
                    Return ONLY a JSON array.

                    Do not return markdown.
                    Do not return explanations.
                    Do not wrap in ```json.
                    """),

                    new UserChatMessage(prompt)
                ],
                new ChatCompletionOptions
                {
                    Temperature = 0
                });

                var text = response.Value.Content
                    .FirstOrDefault()?.Text;

                Console.WriteLine($"RAW AI RESPONSE: {text}");

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var results = JsonSerializer.Deserialize<List<AiBatchResult>>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (results == null)
                    continue;

                foreach (var result in results)
                {
                    var file = batchList
                        .FirstOrDefault(x => x.Id == result.Id);

                    if (file == null)
                        continue;

                    file.AiCategory = result.Category;
                    file.AiSubCategory = result.SubCategory;
                    file.AiDescription = result.Description;
                    file.AiConfidence = (float?)result.Confidence;
                    file.AiProcessed = true;
                }

                Console.WriteLine(
                    $"Processed AI batch: {batchList.Count} files");

                if (onBatchCompleted != null)
                {
                    await onBatchCompleted();
                }

                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI BATCH ERROR: {ex}");

                await Task.Delay(10000);
            }
        }
    }

    public async Task<string> TestAsync()
    {
        var response = await _client.CompleteChatAsync(
        [
            new UserChatMessage("Say hello")
        ]);

        return response.Value.Content
            .FirstOrDefault()?.Text ?? "No response";
    }

    private static bool NeedsAi(MediaFile file)
    {
        return file.RequiresReview;
    }

    private static string BuildBatchPrompt(List<MediaFile> files)
    {
        var items = files.Select(x => new
        {
            x.Id,
            x.FileName,
            x.Type,
            Extension = Path.GetExtension(x.FullPath),

            OcrText = string.IsNullOrWhiteSpace(x.OcrText)
                ? null
                : x.OcrText.Substring(
                    0,
                    Math.Min(x.OcrText.Length, 1000))
        });

        var json = JsonSerializer.Serialize(items);

        return $$"""
        Classify these files.

        You MUST return one result for every file.
        Never skip files.

        Allowed categories:
        - Travel
        - Family
        - Work
        - Screenshots
        - Documents
        - Music
        - Movies
        - Games
        - Memes
        - Receipts
        - Unknown

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