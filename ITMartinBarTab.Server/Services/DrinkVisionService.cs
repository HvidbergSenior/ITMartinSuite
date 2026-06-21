using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Configuration;

namespace ITMartinBarTab.Server.Services;

public sealed record DrinkAnalysis(string Description, decimal SuggestedPrice);

public sealed class DrinkVisionService(IConfiguration configuration)
{
    private static readonly Tool AnalyzeTool = new()
    {
        Name = "analyze_drink",
        Description = "Identify the drink and suggest a price in Danish kroner",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["description"] = JsonSerializer.SerializeToElement(
                    new { type = "string", description = "Short description of the drink, e.g. 'Carlsberg beer' or 'Bottle of red wine'" }),
                ["suggestedPrice"] = JsonSerializer.SerializeToElement(
                    new { type = "number", description = "Suggested price in Danish kroner (DKK)" }),
            },
            Required = ["description", "suggestedPrice"],
        }
    };

    public async Task<DrinkAnalysis?> AnalyzeAsync(byte[] imageBytes, string mimeType = "image/jpeg")
    {
        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var client = new AnthropicClient { ApiKey = apiKey };
        var base64 = Convert.ToBase64String(imageBytes);

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 256,
            System = "You are at a Danish bar or restaurant. Identify the drink in the photo and suggest a realistic price in DKK.",
            Tools = [AnalyzeTool],
            ToolChoice = new ToolChoiceTool { Name = "analyze_drink" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "What drink is this and what would it cost at a Danish bar?" },
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = base64, MediaType = mimeType }
                        }
                    }
                }
            ]
        };

        var response = await client.Messages.Create(request);

        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse)) continue;
            var json = JsonSerializer.Serialize(toolUse.Input);
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            var desc = result.GetProperty("description").GetString() ?? "Drink";
            var price = result.GetProperty("suggestedPrice").GetDecimal();
            return new DrinkAnalysis(desc, price);
        }

        return null;
    }
}
