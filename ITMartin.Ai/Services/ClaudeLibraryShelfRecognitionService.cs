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

public sealed class ClaudeLibraryShelfRecognitionService
    : ILibraryShelfRecognitionService
{
    private const int MaxCacheSize = 200;

    private static readonly ConcurrentDictionary<string, LibraryShelfAnalysisResult>
        Cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportShelfTool = new()
    {
        Name = "report_shelf",
        Description = "Report all visible items identified on the shelf",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["items"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "All visible shelf items",
                        "items": {
                            "type": "object",
                            "properties": {
                                "title":     { "type": "string" },
                                "author":    { "type": "string" },
                                "isbn":      { "type": "string" },
                                "barcode":   { "type": "string" },
                                "mediaType": { "type": "string", "description": "Book, Comic, or Movie" },
                                "confidence":{ "type": "number" }
                            }
                        }
                    }
                    """).RootElement
            },
            Required = ["items"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeLibraryShelfRecognitionService> _logger;

    public ClaudeLibraryShelfRecognitionService(
        IConfiguration configuration,
        ILogger<ClaudeLibraryShelfRecognitionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<LibraryShelfAnalysisResult?> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            const int MaxBytes = 9 * 1024 * 1024;
            if (bytes.Length > MaxBytes)
            {
                using var image = Image.Load(bytes);
                var scale = Math.Sqrt(4.0 * 1024 * 1024 / bytes.Length);
                image.Mutate(x => x.Resize(
                    Math.Max(1, (int)(image.Width * scale)),
                    Math.Max(1, (int)(image.Height * scale))));

                var quality = 80;
                byte[] resized;
                do
                {
                    using var ms = new MemoryStream();
                    await image.SaveAsJpegAsync(ms,
                        new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality },
                        cancellationToken);
                    resized = ms.ToArray();
                    quality -= 10;
                } while (resized.Length > MaxBytes && quality > 10);

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
                MaxTokens = 2048,
                System = """
                    You are an expert library inventory system.
                    Identify every visible book, comic and movie on the shelf.
                    Use only text directly visible in the image.
                    A missing value is better than a guessed one.
                    """,
                Tools = [ReportShelfTool],
                ToolChoice = new ToolChoiceTool { Name = "report_shelf" },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new TextBlockParam
                            {
                                Text = "Identify every visible book, comic and movie on this shelf. Call the report_shelf tool with all items you can identify."
                            },
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

            var response = await _client.Messages.Create(request);

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
            _logger.LogDebug("Claude shelf response: {Json}", json);

            var result = JsonSerializer.Deserialize<LibraryShelfAnalysisResult>(json, JsonOptions);

            if (result is null)
                return null;

            if (Cache.Count >= MaxCacheSize)
                Cache.Clear();

            Cache[cacheKey] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude shelf recognition failed");
            throw;
        }
    }

    private static string CreateHash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
}
