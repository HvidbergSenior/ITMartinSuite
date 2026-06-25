using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinMusicGame.Services;

public record PerformanceScore(
    string Title,
    string Feedback,
    int Commitment,
    int Presence,
    int Points
);

public class ScoringService
{
    private readonly AnthropicClient? _client;

    public ScoringService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public async Task<PerformanceScore> ScorePerformanceAsync(
        string songTitle, string? lyrics, string? transcription,
        string? photoBase64, string playerName)
    {
        if (_client is null) return Fallback(playerName);

        var content = new List<ContentBlockParam>();

        var prompt = new System.Text.StringBuilder();
        prompt.AppendLine($"Sangen er: \"{songTitle}\"");
        prompt.AppendLine($"Sangerens navn: {playerName}");

        if (!string.IsNullOrWhiteSpace(lyrics))
            prompt.AppendLine($"\nDe korrekte sangtekster:\n{lyrics}");

        if (!string.IsNullOrWhiteSpace(transcription))
            prompt.AppendLine($"\nHvad sangeren rent faktisk sang (tale-til-tekst):\n{transcription}");
        else
            prompt.AppendLine("\n(Ingen transskription tilgængelig — vurder ud fra fotos og stil)");

        prompt.AppendLine("""

            Du er en fjollet, energisk musikdommer. Giv en SJOV vurdering på dansk.
            Svar KUN med dette JSON-format:
            {
              "title": "Sjov dansk titel (max 5 ord, f.eks. 'Falsk Mariah Carey' eller 'Badekarsanger Deluxe')",
              "feedback": "2-3 sætninger med sjov, venlig feedback. Nævn specifikke ting.",
              "commitment": 7,
              "presence": 6,
              "points": 45
            }
            Points: 0-100. Vær generøs men sjov.
            """);

        content.Add(new TextBlockParam { Text = prompt.ToString() });

        if (!string.IsNullOrWhiteSpace(photoBase64))
        {
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = photoBase64, MediaType = "image/jpeg" }
            });
        }

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 400,
                Messages = [new() { Role = Role.User, Content = content }]
            });

            foreach (var block in response.Content)
            {
                if (!block.TryPickText(out var tb)) continue;
                var json = tb.Text.Trim();
                if (json.StartsWith("```")) json = string.Join("\n", json.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```")));
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var r = doc.RootElement;
                return new PerformanceScore(
                    r.GetProperty("title").GetString() ?? "Sangfugl",
                    r.GetProperty("feedback").GetString() ?? "",
                    r.GetProperty("commitment").GetInt32(),
                    r.GetProperty("presence").GetInt32(),
                    r.GetProperty("points").GetInt32()
                );
            }
        }
        catch { }

        return Fallback(playerName);
    }

    private static PerformanceScore Fallback(string name) => new(
        "Mystisk Sangfænomen",
        $"{name} leverede en performance der må opleves for at forstås. Juryens øjne er stadig ikke tørre — af en eller anden grund.",
        6, 7, 40
    );
}
