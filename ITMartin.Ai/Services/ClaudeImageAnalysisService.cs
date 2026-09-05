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

    // Same per-image fields as ReportImageTool, but as an array so one call
    // can return a verdict for every image sent in that call. "index" is
    // 1-based and matches the "Image N:" label placed before each image in
    // the message, since that's the only way the model has to say which
    // verdict belongs to which image - there's no positional guarantee
    // otherwise.
    private static readonly Tool ReportImagesBatchTool = new()
    {
        Name = "report_images",
        Description = "Report the analysis of every image in this message, one entry per image",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["results"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "One entry per image, in any order - matched back by index",
                        "items": {
                            "type": "object",
                            "properties": {
                                "index": { "type": "integer", "description": "1-based image number this entry describes, matching its \"Image N:\" label" },
                                "description": { "type": "string", "description": "A concise description of what is in the image" },
                                "tags": { "type": "array", "items": { "type": "string" }, "description": "Relevant tags for the image content" },
                                "confidence": { "type": "number", "description": "Confidence score 0.0-1.0" },
                                "is_blurry": { "type": "boolean", "description": "True if the image is noticeably blurry or out of focus" },
                                "is_solid_color": { "type": "boolean", "description": "True if the image is mostly a single solid color, blank, or near-empty" },
                                "is_meme": { "type": "boolean", "description": "True ONLY if the image's own purpose is a joke/internet-humor format - a meme template, captioned joke image, or similar - where the humor IS the content. False for a real personal/family photo, even if it has a funny caption, sticker, emoji, or was shared through Snapchat/Instagram Story." },
                                "is_screenshot": { "type": "boolean", "description": "True if the image is a screenshot of a phone, computer, or app UI" },
                                "is_chat": { "type": "boolean", "description": "True ONLY if this shows an actual text/SMS/iMessage/WhatsApp conversation THREAD - multiple message bubbles, a contact name, chat UI." }
                            },
                            "required": ["index", "description", "confidence", "is_blurry", "is_solid_color", "is_meme", "is_screenshot", "is_chat"]
                        }
                    }
                    """).RootElement,
            },
            Required = ["results"],
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

    public async Task<IReadOnlyList<AiAnalysisResult>> AnalyzeImagesBatchAsync(IReadOnlyList<string> filePaths)
    {
        var results = new AiAnalysisResult?[filePaths.Count];

        // Load bytes/hash once per file, up front - lets already-cached
        // images skip the API call entirely (same cache the single-image
        // path uses, so a mixed batch of new + previously-seen photos only
        // ever pays for what's actually new) without needing a second pass.
        var pending = new List<(int Index, string Path, byte[] Bytes, string CacheKey)>();
        for (var i = 0; i < filePaths.Count; i++)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePaths[i]);
                var cacheKey = $"image-{CreateHash(bytes)}";
                if (Cache.TryGetValue(cacheKey, out var cached))
                {
                    results[i] = cached;
                    continue;
                }
                pending.Add((i, filePaths[i], bytes, cacheKey));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude image analysis (batch) failed to read {FilePath}", filePaths[i]);
                results[i] = Empty();
            }
        }

        if (pending.Count == 0 || _client is null)
        {
            for (var i = 0; i < results.Length; i++) results[i] ??= Empty();
            return results!;
        }

        try
        {
            var content = new List<ContentBlockParam>
            {
                new TextBlockParam { Text = $"Analyze each of the following {pending.Count} images and call report_images once with one entry per image, using the 1-based index matching its label below." }
            };
            for (var b = 0; b < pending.Count; b++)
            {
                content.Add(new TextBlockParam { Text = $"Image {b + 1}:" });
                content.Add(new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = Convert.ToBase64String(pending[b].Bytes),
                        MediaType = GetMimeType(pending[b].Path),
                    }
                });
            }

            var request = new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                // Scales with batch size - a single fixed cap would either
                // starve a large batch's per-image verdicts or waste tokens
                // on a small one.
                MaxTokens = 200 + (pending.Count * 200),
                System = "You analyze images for photo library management. Be precise about blur, solid-color/blank images, memes, screenshots, and chat/messenger screenshots specifically (is_chat implies is_screenshot). The most important distinction: a real personal/family photo (even one shared via Snapchat/Instagram with a caption, sticker, or username banner overlaid) is NOT a meme and NOT a chat conversation - it stays a real photo. Only set is_meme for actual joke/humor-format content, and only set is_chat for an actual multi-message conversation thread. When in doubt between 'real photo with social-app decoration' and 'meme/chat', prefer treating it as a real photo. You will be shown multiple images, each preceded by an \"Image N:\" label - call report_images exactly once with one results entry per image, tagged with its matching index.",
                Tools = [ReportImagesBatchTool],
                ToolChoice = new ToolChoiceTool { Name = "report_images" },
                Messages = [new() { Role = Role.User, Content = content }]
            };

            var response = await _client.Messages.Create(request);

            ToolUseBlock? toolUse = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
            }

            if (toolUse is null)
            {
                foreach (var p in pending) results[p.Index] = Empty();
                return results!;
            }

            var json = JsonSerializer.Serialize(toolUse.Input);
            _logger.LogDebug("Claude image analysis (batch, {Count} images): {Json}", pending.Count, json);

            using var doc = JsonDocument.Parse(json);
            var byLabelIndex = new Dictionary<int, AiAnalysisResult>();
            if (doc.RootElement.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultsArray.EnumerateArray())
                {
                    if (!item.TryGetProperty("index", out var idxEl)) continue;
                    var labelIndex = idxEl.GetInt32();

                    var result = JsonSerializer.Deserialize<AiAnalysisResult>(item.GetRawText(), JsonOptions) ?? Empty();
                    if (item.TryGetProperty("is_blurry", out var bl))       result.IsBlurry     = bl.GetBoolean();
                    if (item.TryGetProperty("is_solid_color", out var sc2)) result.IsSolidColor = sc2.GetBoolean();
                    if (item.TryGetProperty("is_meme", out var mm))         result.IsMeme       = mm.GetBoolean();
                    if (item.TryGetProperty("is_screenshot", out var ss))   result.IsScreenshot = ss.GetBoolean();
                    if (item.TryGetProperty("is_chat", out var cc))        result.IsChat       = cc.GetBoolean();

                    byLabelIndex[labelIndex] = result;
                }
            }

            for (var b = 0; b < pending.Count; b++)
            {
                var (originalIndex, path, _, cacheKey) = pending[b];
                var result = byLabelIndex.TryGetValue(b + 1, out var r) ? r : Empty();
                result.FullPath = path;
                Cache[cacheKey] = result;
                results[originalIndex] = result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude image analysis (batch of {Count}) failed", pending.Count);
            foreach (var p in pending) results[p.Index] = Empty();
        }

        for (var i = 0; i < results.Length; i++) results[i] ??= Empty();
        return results!;
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
