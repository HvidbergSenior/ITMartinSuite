using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinStarRealms.Server.Services;

// Only called from the explicit "Flere ikoner…" button click on Home - never
// on page load or automatically, so a normal session costs nothing (see
// CLAUDE.md AI cost discipline). Cheap Haiku, one small batched call.
public sealed class EmojiSuggestionService
{
    private readonly AnthropicClient? _client;
    private readonly ILogger<EmojiSuggestionService> _logger;

    public EmojiSuggestionService(IConfiguration configuration, ILogger<EmojiSuggestionService> logger)
    {
        _logger = logger;
        var apiKey = Environment.GetEnvironmentVariable("Claude__ApiKey") ?? configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<List<string>> SuggestAsync(List<string> exclude, CancellationToken ct = default)
    {
        if (_client is null) return [];

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 300,
            System = """
                You pick fun single emoji for a space card game's player avatars.
                Return ONLY a JSON array of 14 single-emoji strings.
                Each must be visually distinct from the others and from the excluded list.
                Never return markdown, never explain, never repeat an excluded emoji.
                """,
            Messages =
            [
                new() { Role = Role.User, Content = "Excluded (already used): " + string.Join(" ", exclude) }
            ]
        };

        try
        {
            var response = await _client.Messages.Create(request, cancellationToken: ct);
            string? text = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var tb)) { text = tb.Text; break; }
            }
            if (string.IsNullOrWhiteSpace(text)) return [];

            var suggestions = JsonSerializer.Deserialize<List<string>>(StripCodeFence(text)) ?? [];
            var excludeSet = exclude.ToHashSet();
            return suggestions.Where(e => !string.IsNullOrWhiteSpace(e) && !excludeSet.Contains(e)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Emoji suggestion request failed");
            return [];
        }
    }

    // Haiku's system prompt says "never return markdown", but it doesn't
    // always listen - a leading/trailing ```json fence is common enough that
    // this should tolerate it rather than hard-failing the request.
    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;

        var withoutOpenFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpenFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpenFence[..closingFenceIndex].Trim()
            : withoutOpenFence.Trim();
    }
}
