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

public sealed class ClaudeCdRecognitionService : ICdRecognitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportCdsTool = new()
    {
        Name = "report_cds",
        Description = "Report every CD visible in the photo",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["cds"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "Every CD visible in the photo (front cover, spine, or back tracklist)",
                        "items": {
                            "type": "object",
                            "properties": {
                                "artist": { "type": "string" },
                                "album":  { "type": "string" },
                                "tracks": {
                                    "type": "array",
                                    "items": { "type": "string" },
                                    "description": "Track titles in order. Use the printed tracklist if visible (back cover). If only the front cover/spine is visible and you recognise the specific album from your own knowledge, you may fill in its known tracklist - but if you don't recognise it and no tracklist is visible, leave this empty rather than guessing."
                                }
                            }
                        }
                    }
                    """).RootElement
            },
            Required = ["cds"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeCdRecognitionService> _logger;

    public ClaudeCdRecognitionService(IConfiguration configuration, ILogger<ClaudeCdRecognitionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<CdRecognitionResult?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

        using (var image = Image.Load(bytes))
        {
            const int MaxWidth = 1920;
            if (image.Width > MaxWidth)
            {
                var ratio = (double)MaxWidth / image.Width;
                image.Mutate(x => x.Resize(MaxWidth, (int)(image.Height * ratio)));
            }

            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms,
                new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 },
                cancellationToken);
            bytes = ms.ToArray();
        }

        var mime = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        var base64 = Convert.ToBase64String(bytes);

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 2048,
            System = """
                You are identifying physical CDs from a photo - could be one CD's front
                cover, its back cover with a printed tracklist, a spine, or a stack/shelf
                of several CDs at once. Identify every distinct CD you can see.
                Use only text actually visible in the image for artist/album names.
                For tracks: prefer a visible printed tracklist. Only fall back to your own
                knowledge of the album's tracklist if you are confident you have correctly
                identified that specific album - a missing tracklist is better than a wrong one.
                """,
            Tools = [ReportCdsTool],
            ToolChoice = new ToolChoiceTool { Name = "report_cds" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam
                        {
                            Text = "Identify every CD visible in this photo and call the report_cds tool."
                        },
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = base64, MediaType = mime }
                        }
                    }
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        }

        if (toolUse is null) return null;

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogInformation("Claude CD recognition response: {Json}", json);

        return JsonSerializer.Deserialize<CdRecognitionResult>(json, JsonOptions);
    }
}
