using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinFamily.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ITMartinFamily.Infrastructure.Services;

public sealed class FamilyClaudeService : IFamilyClaudeService
{
    private static readonly Tool ParseTool = new()
    {
        Name        = "parse_item",
        Description = "Extract the item name and location from a natural language sentence",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["item_name"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The name of the item (e.g. 'nøgler', 'briller', 'pung')" }),
                ["location"]  = JsonSerializer.SerializeToElement(new { type = "string", description = "Where the item is (e.g. 'køkkenbord', 'bilens handskerum')" }),
            },
            Required = ["item_name", "location"],
        },
    };

    private readonly AnthropicClient _client;

    public FamilyClaudeService(IConfiguration config)
    {
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey not configured");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<(string Name, string Location, bool Success)> ParseItemAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model      = Model.ClaudeHaiku4_5,
                MaxTokens  = 256,
                System     = "Du hjælper med at finde ting i hjemmet. Udtræk genstandsnavn og placering fra brugerens besked. Kald altid parse_item.",
                Tools      = [ParseTool],
                ToolChoice = new ToolChoiceTool { Name = "parse_item" },
                Messages   = [new() { Role = Role.User, Content = input }]
            }, cancellationToken: ct);

            foreach (var block in response.Content)
            {
                if (!block.TryPickToolUse(out var toolUse)) continue;
                var raw = JsonSerializer.Deserialize<ParsedRaw>(JsonSerializer.Serialize(toolUse.Input),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (raw is not null && !string.IsNullOrWhiteSpace(raw.ItemName) && !string.IsNullOrWhiteSpace(raw.Location))
                    return (raw.ItemName, raw.Location, true);
            }
        }
        catch { }
        return ("", "", false);
    }

    public async Task<(string Name, string Location, bool Success)> AnalyzePhotoAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model      = Model.ClaudeHaiku4_5,
                MaxTokens  = 256,
                System     = "Du kigger på billeder og identificerer genstanden og dens placering. Kald altid parse_item.",
                Tools      = [ParseTool],
                ToolChoice = new ToolChoiceTool { Name = "parse_item" },
                Messages   =
                [
                    new()
                    {
                        Role    = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new TextBlockParam { Text = "Hvad er på billedet, og hvor befinder det sig?" },
                            new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(imageBytes), MediaType = mimeType } }
                        }
                    }
                ]
            }, cancellationToken: ct);

            foreach (var block in response.Content)
            {
                if (!block.TryPickToolUse(out var toolUse)) continue;
                var raw = JsonSerializer.Deserialize<ParsedRaw>(JsonSerializer.Serialize(toolUse.Input),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (raw is not null && !string.IsNullOrWhiteSpace(raw.ItemName) && !string.IsNullOrWhiteSpace(raw.Location))
                    return (raw.ItemName, raw.Location, true);
            }
        }
        catch { }
        return ("", "", false);
    }

    private sealed class ParsedRaw
    {
        public string? ItemName { get; set; }
        public string? Location { get; set; }
    }
}
