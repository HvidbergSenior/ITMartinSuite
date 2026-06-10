using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Ai.Services;

public abstract class OpenAiServiceBase
{
    protected readonly
        ChatClient
        Client;

    protected OpenAiServiceBase(
        IConfiguration configuration,
        string model = "gpt-4.1")
    {
        var apiKey =
            configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception(
                "Missing OpenAI API key");
        }

        Client =
            new ChatClient(
                model,
                apiKey);
    }

    protected static string CreateHash(
        byte[] bytes)
    {
        using var sha =
            SHA256.Create();

        return Convert.ToHexString(
            sha.ComputeHash(bytes));
    }

    protected static string GetMimeType(
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
}