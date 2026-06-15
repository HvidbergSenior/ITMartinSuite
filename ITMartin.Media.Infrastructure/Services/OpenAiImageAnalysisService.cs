using System.Security.Cryptography;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Configuration;

namespace ITMartin.Media.Infrastructure.Services;

public sealed class OpenAiImageAnalysisService
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
        Description = "Report the image analysis result",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["description"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Concise description of the image" }),
                ["tags"] = JsonDocument.Parse("""
                    { "type": "array", "items": { "type": "string" }, "description": "Relevant content tags" }
                    """).RootElement,
                ["confidence"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Confidence score 0.0–1.0" }),
            },
            Required = ["description", "confidence"],
        },
    };

    private readonly AnthropicClient _client;

    public OpenAiImageAnalysisService(IConfiguration configuration)
    {
        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

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
                Model = Model.ClaudeOpus4_8,
                MaxTokens = 512,
                System = "You analyze images and return structured descriptions.",
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
            Console.WriteLine($"CLAUDE RESPONSE: {json}");

            var result = JsonSerializer.Deserialize<AiAnalysisResult>(json, JsonOptions);

            if (result is null)
                return Empty();

            Cache[cacheKey] = result;
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CLAUDE ERROR: {ex}");
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
