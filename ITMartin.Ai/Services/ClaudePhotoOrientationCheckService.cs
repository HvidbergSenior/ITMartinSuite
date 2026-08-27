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

public sealed class ClaudePhotoOrientationCheckService : IPhotoOrientationCheckService
{
    // Multiple photos in ONE call, not one call per photo - see
    // feedback_ai_cost_ceiling memory. 20 keeps a single request's image
    // payload and output token count reasonable.
    public const int BatchSize = 20;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Tool ReportOrientationsTool = new()
    {
        Name = "report_orientations",
        Description = "Report the orientation check result for every numbered photo in this batch",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["results"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "One entry per photo, in the same order/numbering they were shown",
                        "items": {
                            "type": "object",
                            "properties": {
                                "index": { "type": "integer", "description": "1-based photo number, matching the order shown" },
                                "needsRotation": { "type": "boolean", "description": "True if the photo is sideways or upside-down and needs correcting" },
                                "degreesNeeded": { "type": "integer", "enum": [0, 90, 180, 270], "description": "Clockwise degrees to rotate to fix it - 0 if needsRotation is false" },
                                "reasoning": { "type": "string", "description": "Brief note on what told you the orientation - e.g. 'faces sideways', 'horizon tilted 90 degrees', 'text upside down'" }
                            },
                            "required": ["index", "needsRotation", "degreesNeeded"]
                        }
                    }
                    """).RootElement
            },
            Required = ["results"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudePhotoOrientationCheckService> _logger;

    public ClaudePhotoOrientationCheckService(IConfiguration configuration, ILogger<ClaudePhotoOrientationCheckService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<List<PhotoOrientationResult>> CheckBatchAsync(
        IReadOnlyList<(string FullPath, string RelativePath)> images,
        CancellationToken cancellationToken = default)
    {
        if (images.Count == 0) return [];
        if (images.Count > BatchSize)
            throw new ArgumentException($"Batch of {images.Count} exceeds max BatchSize of {BatchSize} - caller must chunk.");

        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = $"Check these {images.Count} photos for orientation. For each, call out in report_orientations " +
                       "whether it needs rotating (sideways or upside-down) and by how many degrees clockwise to fix it. " +
                       "Most photos are already correct - only flag needsRotation=true when you're confident it's actually wrong."
            }
        };

        foreach (var (index, image) in images.Select((img, i) => (i + 1, img)))
        {
            var bytes = await File.ReadAllBytesAsync(image.FullPath, cancellationToken);

            // Small resize - judging orientation (faces, horizon, text) needs
            // far less detail than content analysis, and this runs across
            // thousands of photos, so keeping per-image tokens low matters
            // more here than in the other AI services in this project.
            using (var img = Image.Load(bytes))
            {
                const int MaxDim = 768;
                if (img.Width > MaxDim || img.Height > MaxDim)
                {
                    var ratio = Math.Min((double)MaxDim / img.Width, (double)MaxDim / img.Height);
                    img.Mutate(x => x.Resize((int)(img.Width * ratio), (int)(img.Height * ratio)));
                }
                using var ms = new MemoryStream();
                await img.SaveAsJpegAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 }, cancellationToken);
                bytes = ms.ToArray();
            }

            content.Add(new TextBlockParam { Text = $"Photo {index}:" });
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = "image/jpeg" }
            });
        }

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 4096,
            System = "You are checking family photos for correct orientation before they're delivered to a customer. " +
                     "Judge from faces, horizons, buildings, text, and general scene composition. Be conservative - " +
                     "a photo that's merely a tilted/artistic angle is NOT the same as sideways/upside-down.",
            Tools = [ReportOrientationsTool],
            ToolChoice = new ToolChoiceTool { Name = "report_orientations" },
            Messages = [new() { Role = Role.User, Content = content }],
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        }

        if (toolUse is null) return [];

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogInformation("Photo orientation batch response ({Count} photos): {Json}", images.Count, json);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var resultsElement))
            return [];

        var results = new List<PhotoOrientationResult>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            var index = item.GetProperty("index").GetInt32();
            if (index < 1 || index > images.Count) continue;

            results.Add(new PhotoOrientationResult
            {
                RelativePath  = images[index - 1].RelativePath,
                NeedsRotation = item.TryGetProperty("needsRotation", out var nr) && nr.GetBoolean(),
                DegreesNeeded = item.TryGetProperty("degreesNeeded", out var dn) ? dn.GetInt32() : 0,
                Reasoning     = item.TryGetProperty("reasoning", out var r) ? r.GetString() ?? "" : "",
            });
        }

        return results;
    }
}
