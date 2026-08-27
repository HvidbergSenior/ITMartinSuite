using System.Security.Cryptography;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class ClaudeImageAnalysisService
    : IImageAnalysisService
{
    private static readonly Dictionary<string, AiAnalysisResult> Cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportImageTool = new()
    {
        Name = "report_image",
        Description = "Report the analysis of the image",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["description"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "A concise description of what is in the image" }),
                ["tags"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "string" }, "description": "Relevant tags for the image content" }
                    """).RootElement,
                ["confidence"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Confidence score 0.0–1.0" }),
                ["is_blurry"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "True if the image is noticeably blurry or out of focus" }),
                ["is_solid_color"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "True if the image is mostly a single solid color, blank, or near-empty" }),
                ["is_meme"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "True ONLY if the image's own purpose is a joke/internet-humor format - a meme template, captioned joke image, or similar - where the humor IS the content. False for a real personal/family photo, even if it has a funny caption, sticker, emoji, or was shared through Snapchat/Instagram Story - a photo of a real moment (a baby, a family event, a selfie) stays false here no matter what text is overlaid on it." }),
                ["is_screenshot"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "True if the image is a screenshot of a phone, computer, or app UI (status bar, app chrome, buttons visible)" }),
                ["is_chat"] = JsonSerializer.SerializeToElement(
                    new { type = "boolean", description = "True ONLY if this shows an actual text/SMS/iMessage/WhatsApp conversation THREAD - multiple message bubbles going back and forth between people, a contact name, chat UI. False for a single photo with a caption/sticker/username banner (Snapchat, Instagram Story, or similar photo-sharing format) - that is a real photo that was shared, not a chat conversation, even though it came through a messaging app." }),
            },
            Required = ["description", "confidence", "is_blurry", "is_solid_color", "is_meme", "is_screenshot", "is_chat"],
        },
    };

    private readonly AnthropicClient? _client;
    private readonly ILogger<ClaudeImageAnalysisService> _logger;

    public ClaudeImageAnalysisService(
        IConfiguration configuration,
        ILogger<ClaudeImageAnalysisService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
            _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<AiAnalysisResult> AnalyzeImageAsync(string filePath)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var cacheKey = $"image-{CreateHash(bytes)}";

            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var mime = GetMimeType(filePath);
            var base64 = Convert.ToBase64String(bytes);

            var request = new MessageCreateParams
            {
                // Haiku, not Opus - this runs once per photo in a library that can run
                // into the tens of thousands of files. See feedback_package3_cost_ceiling
                // memory: AI features must never cost thousands of dollars to run.
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 512,
                System = "You analyze images for photo library management. Be precise about blur, solid-color/blank images, memes, screenshots, and chat/messenger screenshots specifically (is_chat implies is_screenshot). The most important distinction: a real personal/family photo (even one shared via Snapchat/Instagram with a caption, sticker, or username banner overlaid) is NOT a meme and NOT a chat conversation - it stays a real photo. Only set is_meme for actual joke/humor-format content, and only set is_chat for an actual multi-message conversation thread. When in doubt between 'real photo with social-app decoration' and 'meme/chat', prefer treating it as a real photo. Always call report_image.",
                Tools = [ReportImageTool],
                ToolChoice = new ToolChoiceTool { Name = "report_image" },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new TextBlockParam { Text = "Analyze this image and call the report_image tool." },
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

            if (_client is null)
                return Empty();

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
                return Empty();

            var json = JsonSerializer.Serialize(toolUse.Input);
            _logger.LogDebug("Claude image analysis: {Json}", json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = JsonSerializer.Deserialize<AiAnalysisResult>(json, JsonOptions);
            if (result is not null)
            {
                if (root.TryGetProperty("is_blurry", out var b))     result.IsBlurry     = b.GetBoolean();
                if (root.TryGetProperty("is_solid_color", out var s)) result.IsSolidColor = s.GetBoolean();
                if (root.TryGetProperty("is_meme", out var m))        result.IsMeme       = m.GetBoolean();
                if (root.TryGetProperty("is_screenshot", out var sc)) result.IsScreenshot = sc.GetBoolean();
                if (root.TryGetProperty("is_chat", out var c))        result.IsChat       = c.GetBoolean();
            }

            if (result is null)
                return Empty();

            Cache[cacheKey] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude image analysis failed for {FilePath}", filePath);
            return Empty();
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

    private static AiAnalysisResult Empty() => new()
    {
        Description = "Unknown",
        Tags = [],
        Confidence = 0
    };
}
