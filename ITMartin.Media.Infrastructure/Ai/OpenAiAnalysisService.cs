using System.Text.Json;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class OpenAiAnalysisService
    : IAiAnalysisService
{
    private readonly ChatClient _client;

    public OpenAiAnalysisService(
        IConfiguration configuration)
    {
        var apiKey =
            configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception(
                "Missing OpenAI API key");
        }

        _client = new ChatClient(
            model: "gpt-4.1-mini",
            apiKey: apiKey);
    }

    public async Task<AiAnalysisResult>
        AnalyzeImageAsync(string filePath)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(filePath);

            var ext =
                Path.GetExtension(filePath)
                    .ToLower();

            var mime =
                ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    ".heic" => "image/heic",
                    ".heif" => "image/heif",
                    ".avif" => "image/avif",
                    _ => "image/jpeg"
                };

            var response =
                await _client.CompleteChatAsync(
                [
                    new SystemChatMessage("""
                    You analyze images.

                    Return ONLY valid JSON.

                    Example:

                    {
                      "description": "A child skiing in snow",
                      "tags": ["skiing", "snow", "child"],
                      "confidence": 0.95
                    }
                    """),

                    new UserChatMessage(
                    [
                        ChatMessageContentPart.CreateTextPart(
                            "Analyze this image"),

                        ChatMessageContentPart
                            .CreateImagePart(
                                BinaryData.FromBytes(bytes),
                                mime)
                    ])
                ]);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

            Console.WriteLine(
                $"OPENAI RESPONSE: {text}");

            if (string.IsNullOrWhiteSpace(text))
            {
                return Empty();
            }

            var result =
                JsonSerializer.Deserialize<
                    AiAnalysisResult>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return result ?? Empty();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"OPENAI ERROR: {ex}");

            return Empty();
        }
    }

    private static AiAnalysisResult Empty()
    {
        return new AiAnalysisResult
        {
            Description = "Unknown",
            Tags = [],
            Confidence = 0
        };
    }
}