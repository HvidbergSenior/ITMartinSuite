using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class ClaudeShelfScanService
    : IShelfScanService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool ReportProductsTool = new()
    {
        Name = "report_products",
        Description = "Report every product identified across the shelf photos",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["products"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "Every distinct product visible across all the shelf photos",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name":         { "type": "string", "description": "Full product name, or the brand's well-known name if the exact variant is unclear" },
                                "manufacturer": { "type": "string", "description": "Brand/manufacturer, omit if unknown" },
                                "category":     { "type": "string", "description": "Rødvin, Hvidvin, Rosé, Øl, Spiritus, Kaffe, Te, Pasta, Ris, Konserves, Mejeri, Kød, Snacks, Rengøring, Hygiejne, or another fitting category" }
                            },
                            "required": ["name", "category"]
                        }
                    }
                    """).RootElement
            },
            Required = ["products"],
        },
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeShelfScanService> _logger;

    public ClaudeShelfScanService(
        IConfiguration configuration,
        ILogger<ClaudeShelfScanService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude API key");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ShelfScanResult> AnalyzeAsync(
        List<string> base64Images,
        CancellationToken cancellationToken = default)
    {
        if (base64Images.Count == 0)
            return new ShelfScanResult();

        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "Identify every visible product across these shelf photos and call the report_products tool."
            }
        };
        foreach (var image in base64Images)
            content.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = image, MediaType = "image/jpeg" } });

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 4096,
            System = """
                Du er en indkøbsassistent. Analyser disse hyllebilleder og identificer ALLE synlige produkter.

                Billederne er taget på ~1-1,5 meters afstand – brug derfor:
                - Logo, flaskeform, emballagefarve og design til at genkende produkter, selv om tekst er uskarpt
                - Din viden om kendte mærker (Carlsberg, Tuborg, Heineken, Arla, Lurpak, Nescafé, Jacobs, Lay's osv.)
                - Giv dit bedste bud på navn – spring IKKE produkter over blot fordi teksten er svær at læse
                """,
            Tools = [ReportProductsTool],
            ToolChoice = new ToolChoiceTool { Name = "report_products" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = content
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

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
            throw new InvalidOperationException("Claude did not call the report_products tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Claude shelf scan response: {Json}", json);

        var result = JsonSerializer.Deserialize<ShelfScanResult>(json, JsonOptions);

        return result ?? new ShelfScanResult();
    }
}
