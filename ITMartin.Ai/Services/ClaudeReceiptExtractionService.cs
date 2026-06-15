using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class ClaudeReceiptExtractionService
    : IReceiptExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportReceiptTool = new()
    {
        Name = "report_receipt",
        Description = "Report the extracted receipt data",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["merchantName"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Store or merchant name" }),
                ["purchaseDate"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Date of purchase in ISO 8601 format" }),
                ["totalAmount"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Total amount paid" }),
                ["vatAmount"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "VAT / tax amount" }),
                ["currency"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Currency code e.g. DKK, EUR, USD" }),
                ["items"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "Individual line items on the receipt",
                        "items": {
                            "type": "object",
                            "properties": {
                                "description": { "type": "string" },
                                "amount":      { "type": "number" }
                            }
                        }
                    }
                    """).RootElement
            },
            Required = [],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeReceiptExtractionService> _logger;

    public ClaudeReceiptExtractionService(
        IConfiguration configuration,
        ILogger<ClaudeReceiptExtractionService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ReceiptExtractionResult> ExtractAsync(
        string receiptText,
        CancellationToken cancellationToken = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1024,
            System = """
                You are a receipt extraction system.
                Extract structured data from the receipt text provided.
                Omit fields you cannot determine — never guess.
                """,
            Tools = [ReportReceiptTool],
            ToolChoice = new ToolChoiceTool { Name = "report_receipt" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"""
                        Extract the receipt data from the following text and call the report_receipt tool.

                        Receipt text:

                        {receiptText}
                        """
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
            throw new InvalidOperationException("Claude did not call the report_receipt tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude receipt response: {Json}", json);

        var result = JsonSerializer.Deserialize<ReceiptExtractionResult>(json, JsonOptions);

        return result ?? throw new InvalidOperationException("Failed to deserialize receipt result.");
    }
}
