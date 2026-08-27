using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinStarRealms.Server.Services;

// Only called from the explicit "🤖 Spørg" button in a live game's rules
// panel - never automatic. Cheap Haiku, one short call per question (see
// CLAUDE.md AI cost discipline).
public sealed class RulesQuestionService
{
    private readonly AnthropicClient? _client;
    private readonly ILogger<RulesQuestionService> _logger;

    public RulesQuestionService(IConfiguration configuration, ILogger<RulesQuestionService> logger)
    {
        _logger = logger;
        var apiKey = Environment.GetEnvironmentVariable("Claude__ApiKey") ?? configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string?> AskAsync(string rulesetName, string rulesetDescription, string question, CancellationToken ct = default)
    {
        if (_client is null || string.IsNullOrWhiteSpace(question)) return null;

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 400,
            System = $"""
                Du hjælper spillere med regelspørgsmål midt i et parti "{rulesetName}"
                (kortspillet Star Realms, evt. med husregler).

                Regelsættets egen beskrivelse/husregler for netop dette parti:
                {(string.IsNullOrWhiteSpace(rulesetDescription) ? "(ingen særlig beskrivelse angivet)" : rulesetDescription)}

                Svar KORT og PRÆCIST på dansk (maks 3-4 sætninger). Brug regelsættets
                egen beskrivelse hvis den er relevant for spørgsmålet, ellers svar ud
                fra almindelige, velkendte regler for Star Realms. Sig ærligt fra hvis
                du er usikker, fremfor at opfinde en regel.
                """,
            Messages = [new() { Role = Role.User, Content = question }]
        };

        try
        {
            var response = await _client.Messages.Create(request, cancellationToken: ct);
            foreach (var block in response.Content)
                if (block.TryPickText(out var tb) && !string.IsNullOrWhiteSpace(tb.Text))
                    return tb.Text.Trim();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rules question failed");
            return null;
        }
    }
}
