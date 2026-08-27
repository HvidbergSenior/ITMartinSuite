using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Ai.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai.Services;

// Free-form Q&A over a clip's still frames, for Vlog Studio's "spørg AI"
// panel - explicitly on-demand (a button the user presses), never automatic
// or per-file bulk, so a plain per-question call is fine cost-wise (see
// feedback_ai_cost_ceiling memory: that constraint is about per-file bulk
// passes, not occasional user-initiated questions like this one).
public sealed class ClaudeVideoAssistantService : IVideoAssistantService
{
    private readonly AnthropicClient? _client;
    private readonly ILogger<ClaudeVideoAssistantService> _logger;

    public ClaudeVideoAssistantService(IConfiguration configuration, ILogger<ClaudeVideoAssistantService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string> AskAsync(string question, IReadOnlyList<string> frameImagePaths, CancellationToken cancellationToken = default)
    {
        if (_client is null) return "AI er ikke sat op (mangler Claude API-nøgle).";
        if (frameImagePaths.Count == 0) return "Ingen billeder at analysere.";

        try
        {
            var content = new List<ContentBlockParam>
            {
                new TextBlockParam { Text = question }
            };

            foreach (var path in frameImagePaths)
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                content.Add(new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = Convert.ToBase64String(bytes),
                        MediaType = "image/jpeg",
                    }
                });
            }

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 1024,
                System = "Du hjælper en bruger der redigerer sine private videoklip i et vlog-redigeringsværktøj. " +
                         "Du får ét eller flere still-billeder udtrukket fra klip (før/efter en effekt, eller flere klip) " +
                         "og et spørgsmål. Svar kort, konkret og på dansk. Hvis billederne ligner hinanden meget, sig det - " +
                         "gæt ikke på forskelle der ikke er der.",
                Messages = [new() { Role = Role.User, Content = content }]
            }, cancellationToken);

            foreach (var block in response.Content)
                if (block.TryPickText(out var tb)) return tb.Text.Trim();

            return "Fik ikke noget svar fra AI.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video assistant question failed");
            return $"Noget gik galt: {ex.Message}";
        }
    }
}
