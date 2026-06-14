using System.Collections.Concurrent;
using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Ai.Services;

public sealed class OpenAiLibraryShelfRecognitionService
    : OpenAiServiceBase,
        IOpenAiLibraryShelfRecognitionService
{
    private static readonly
        ConcurrentDictionary<string, LibraryShelfAnalysisResult>
        Cache = new();

    public OpenAiLibraryShelfRecognitionService(
        IConfiguration configuration)
        : base(configuration)
    {
    }

    public async Task<LibraryShelfAnalysisResult?>
        AnalyzeAsync(
            string filePath,
            CancellationToken cancellationToken)
    {
        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    filePath,
                    cancellationToken);

            var cacheKey =
                CreateHash(bytes);

            if (Cache.TryGetValue(
                    cacheKey,
                    out var cached))
            {
                return cached;
            }

            var mime =
                GetMimeType(filePath);

            var messages =
                new List<ChatMessage>
                {
                    BuildSystemPrompt(),

                    BuildUserPrompt(
                        bytes,
                        mime)
                };

            var options =
                new ChatCompletionOptions
                {
                    Temperature = 0,

                    ResponseFormat =
                        ChatResponseFormat
                            .CreateJsonObjectFormat()
                };

            var response =
                await Client.CompleteChatAsync(
                    messages,
                    options,
                    cancellationToken);

            var text =
                response.Value.Content
                    .FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            Console.WriteLine("=== AI RESPONSE ===");
            Console.WriteLine(text);
            Console.WriteLine("===================");
            var result =
                JsonSerializer.Deserialize<
                    LibraryShelfAnalysisResult>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result is null)
            {
                return null;
            }

            Cache[cacheKey] =
                result;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"OpenAI recognition failed: {ex}");

            throw;
        }
    }

    private SystemChatMessage BuildSystemPrompt()
    {
        return new SystemChatMessage(
            """
            You are an expert library inventory system.

            Analyze a bookshelf image.

            Identify all visible items.

            Supported media types:

            - Book
            - Comic
            - Movie

            For each visible item extract:

            - title
            - author
            - isbn
            - barcode
            - mediaType

            RULES

            - Never guess.
            - Use only visible text.
            - If uncertain return null.
            - A missing value is better than an incorrect value.
            - Return every visible item you can identify.

            RETURN JSON ONLY

            {
              "items": [
                {
                  "title": null,
                  "author": null,
                  "isbn": null,
                  "barcode": null,
                  "mediaType": null,
                  "confidence": 0.0
                }
              ]
            }
            """);
    }
    private UserChatMessage BuildUserPrompt(
        byte[] bytes,
        string mime)
    {
        return new UserChatMessage(
        [
            ChatMessageContentPart.CreateTextPart(
                """
                Analyze this shelf image.

                Identify all visible books,
                comics and movies.

                Extract:

                - title
                - author
                - isbn
                - barcode
                - mediaType

                Return JSON only.
                """),

            ChatMessageContentPart.CreateImagePart(
                BinaryData.FromBytes(bytes),
                mime,
                ChatImageDetailLevel.High)
        ]);
    }
}