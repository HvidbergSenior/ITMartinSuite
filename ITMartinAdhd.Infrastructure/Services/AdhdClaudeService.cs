using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinAdhd.Application.Interfaces;
using ITMartinAdhd.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinAdhd.Infrastructure.Services;

public sealed class AdhdClaudeService : IAdhdClaudeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ParseItemTool = new()
    {
        Name = "parse_item",
        Description = "Extract the item name and location from a natural language sentence",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["item_name"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "The name of the item being stored (e.g. 'keys', 'passport', 'glasses')" }),
                ["location"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Where the item is located (e.g. 'kitchen counter', 'bedroom dresser', 'car glove box')" }),
            },
            Required = ["item_name", "location"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<AdhdClaudeService> _logger;

    public AdhdClaudeService(
        IConfiguration configuration,
        ILogger<AdhdClaudeService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ParsedItemModel> ParseNaturalLanguageAsync(string input)
    {
        try
        {
            var request = new MessageCreateParams
            {
                Model = "claude-haiku-4-5-20251001",
                MaxTokens = 256,
                System = "You extract item names and locations from natural language. Always call the parse_item tool.",
                Tools = [ParseItemTool],
                ToolChoice = new ToolChoiceTool { Name = "parse_item" },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = input
                    }
                ]
            };

            var response = await _client.Messages.Create(request);

            foreach (var block in response.Content)
            {
                if (!block.TryPickToolUse(out var toolUse)) continue;

                var json = JsonSerializer.Serialize(toolUse.Input);
                _logger.LogDebug("Claude ADHD parse: {Json}", json);

                var parsed = JsonSerializer.Deserialize<ParsedItemRaw>(json, JsonOptions);
                if (parsed is null) break;

                return new ParsedItemModel
                {
                    ItemName = parsed.ItemName ?? "",
                    Location = parsed.Location ?? "",
                    Success = !string.IsNullOrWhiteSpace(parsed.ItemName)
                              && !string.IsNullOrWhiteSpace(parsed.Location),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude ADHD parse failed for input: {Input}", input);
        }

        return new ParsedItemModel { Success = false };
    }

    private sealed class ParsedItemRaw
    {
        public string? ItemName { get; set; }
        public string? Location { get; set; }
    }
}
