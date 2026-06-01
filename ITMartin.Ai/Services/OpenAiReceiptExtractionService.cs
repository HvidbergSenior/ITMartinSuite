using System.Text.Json;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace ITMartin.Ai.Services;

public sealed class OpenAiReceiptExtractionService
    : OpenAiServiceBase,
        IReceiptExtractionService
{
    public OpenAiReceiptExtractionService(
        IConfiguration configuration)
        : base(configuration)
    {
    }

    public async Task<ReceiptExtractionResult>
        ExtractAsync(
            string receiptText,
            CancellationToken cancellationToken = default)
    {
        var messages =
            new List<ChatMessage>
            {
                new SystemChatMessage(
                    """
                    You are a receipt extraction system.

                    Return ONLY valid JSON.
                    """
                ),

                new UserChatMessage(
                    $"""
                    Extract:

                    - MerchantName
                    - PurchaseDate
                    - TotalAmount
                    - VatAmount
                    - Currency

                    Receipt text:

                    {receiptText}
                    """)
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

        var json =
            response.Value.Content
                .FirstOrDefault()
                ?.Text;

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "OpenAI returned empty response.");
        }

        var result =
            JsonSerializer.Deserialize<
                ReceiptExtractionResult>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result is null)
        {
            throw new InvalidOperationException(
                "Failed to deserialize receipt result.");
        }

        return result;
    }
}