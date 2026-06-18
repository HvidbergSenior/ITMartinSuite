using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class ClaudeAuthorSimilarityService : IAuthorSimilarityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Tool SuggestAuthorsTool = new()
    {
        Name = "suggest_authors",
        Description = "Suggest similar authors the user might enjoy",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["suggestions"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "authorName": { "type": "string" },
                                "reason":     { "type": "string", "description": "One sentence why this author matches the library" },
                                "genre":      { "type": "string" }
                            },
                            "required": ["authorName", "reason", "genre"]
                        }
                    }
                    """).RootElement
            },
            Required = []
        }
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeAuthorSimilarityService> _logger;

    public ClaudeAuthorSimilarityService(
        IConfiguration configuration,
        ILogger<ClaudeAuthorSimilarityService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"]
            ?? throw new InvalidOperationException("Missing Claude API key");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<List<AuthorSuggestion>> GetSimilarAuthorsAsync(
        IEnumerable<string> authorsInLibrary,
        CancellationToken cancellationToken = default)
    {
        var authorList = string.Join(", ", authorsInLibrary.Distinct().Take(30));

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 512,
            System = "You are a book recommendation expert. Based on the authors the user already has, suggest 5 authors they might enjoy next.",
            Tools = [SuggestAuthorsTool],
            ToolChoice = new ToolChoiceTool { Name = "suggest_authors" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"My library contains works by: {authorList}\n\nSuggest 5 authors I should explore next."
                }
            ]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        }

        if (toolUse is null) return [];

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Author similarity response: {Json}", json);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("suggestions", out var arr)) return [];

        return JsonSerializer.Deserialize<List<AuthorSuggestion>>(arr.GetRawText(), JsonOptions) ?? [];
    }
}
