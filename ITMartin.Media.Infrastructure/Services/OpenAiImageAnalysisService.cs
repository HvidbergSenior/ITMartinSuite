using System.Security.Cryptography;
using System.Text.Json;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Media.Infrastructure.Services;

public sealed class OpenAiImageAnalysisService
    : IImageAnalysisService
{
    private readonly ChatClient _client;

    private static readonly Dictionary<string, object>
        Cache = new();

    public OpenAiImageAnalysisService(
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
        AnalyzeImageAsync(
            string filePath)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath);

            var cacheKey =
                CreateHash(bytes);

            if (Cache.TryGetValue(
                    $"image-{cacheKey}",
                    out var cached))
            {
                return (AiAnalysisResult)cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
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
                                mime,
                                ChatImageDetailLevel.High)
                    ])
                };

            var options =
                new ChatCompletionOptions
                {
                    ResponseFormat =
                        ChatResponseFormat
                            .CreateJsonObjectFormat()
                };

            var response =
                await _client.CompleteChatAsync(
                    messages,
                    options);

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

            if (result == null)
            {
                return Empty();
            }

            Cache[$"image-{cacheKey}"] =
                result;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"OPENAI ERROR: {ex}");

            return Empty();
        }
    }

    private static string CreateHash(
        byte[] bytes)
    {
        using var sha =
            SHA256.Create();

        var hash =
            sha.ComputeHash(bytes);

        return Convert.ToHexString(hash);
    }

    private static string GetMimeType(
        string filePath)
    {
        var ext =
            Path.GetExtension(filePath)
                .ToLowerInvariant();

        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
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