using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

public sealed class ClaudeEmailRelevanceService : IEmailRelevanceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Tool ScoreEmailsTool = new()
    {
        Name = "score_emails",
        Description = "Score each email for personal relevance and whether it needs a response",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["results"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "messageId":      { "type": "string" },
                                "needsResponse":  { "type": "boolean", "description": "True only if the recipient personally needs to reply or act - not newsletters, receipts, automated notices, or FYI-only threads" },
                                "relevanceScore": { "type": "integer", "description": "0-100, how relevant this is to the user's stated priorities. Marketing/spam/newsletters unrelated to those priorities should score low." },
                                "reasoning":      { "type": "string", "description": "One short sentence explaining the score" }
                            },
                            "required": ["messageId", "needsResponse", "relevanceScore", "reasoning"]
                        }
                    }
                    """).RootElement
            },
            Required = ["results"]
        }
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeEmailRelevanceService> _logger;

    public ClaudeEmailRelevanceService(
        IConfiguration configuration,
        ILogger<ClaudeEmailRelevanceService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"]
            ?? throw new InvalidOperationException("Missing Claude API key");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<List<EmailRelevanceResult>> ScoreBatchAsync(
        IReadOnlyList<EmailSummary> emails,
        string userProfile,
        CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0) return [];

        var emailsBlock = string.Join("\n\n", emails.Select((e, i) =>
            $"[{i}] messageId={e.MessageId}\nFrom: {e.From}\nSubject: {e.Subject}\nReceived: {e.ReceivedAt:yyyy-MM-dd HH:mm}\nSnippet: {e.Snippet}"));

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 4096,
            System =
                "You triage a personal inbox. The user told you what matters to them - use that " +
                "to judge relevance, not generic mail-client categories like Outlook's Focused Inbox " +
                "or Gmail's tabs. Score every email you're given; never skip one.\n\n" +
                $"What matters to this user: {userProfile}",
            Tools = [ScoreEmailsTool],
            ToolChoice = new ToolChoiceTool { Name = "score_emails" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Score these {emails.Count} emails:\n\n{emailsBlock}"
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
        _logger.LogDebug("Email relevance response: {Json}", json);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var arr)) return [];

        return JsonSerializer.Deserialize<List<EmailRelevanceResult>>(arr.GetRawText(), JsonOptions) ?? [];
    }
}
