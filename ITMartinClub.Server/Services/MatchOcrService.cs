using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinClub.Server.Services;

public sealed record ScoreboardRow(string Name, int Kills, int Deaths);

// Reads an R6 Siege scoreboard screenshot (shows both teams at once) and
// returns whoever's name/kills/deaths were legibly readable. Deliberately
// asked to drop a row rather than guess a blurry number - the UI always
// shows these as editable, pre-filled values for a human to confirm before
// anything is saved, same trust model as the receipt scanner.
public sealed class MatchOcrService
{
    private static readonly Tool ReportScoreboardTool = new()
    {
        Name = "report_scoreboard",
        Description = "Report the players read from the scoreboard screenshot",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["players"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "description": "One entry per player row that was clearly legible. Skip a row entirely rather than guess if the name or numbers are blurry/cut off.",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name":   { "type": "string", "description": "The player's in-game name exactly as shown" },
                                "kills":  { "type": "integer" },
                                "deaths": { "type": "integer" }
                            },
                            "required": ["name", "kills", "deaths"]
                        }
                    }
                    """).RootElement
            },
            Required = ["players"],
        },
    };

    private readonly AnthropicClient? _client;

    public MatchOcrService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<List<ScoreboardRow>> ReadScoreboardAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        if (_client is null) return [];

        var base64 = Convert.ToBase64String(imageBytes);

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1024,
            System = """
                You read Rainbow Six Siege end-of-match scoreboard screenshots. The screenshot typically
                shows two teams (often 5 players each), each row with a player name, kills, and deaths.
                Report every row you can read clearly. If a name or number is blurry, cut off, or
                ambiguous, omit that whole row rather than guessing - the numbers you report will be
                trusted and saved, so wrong data is worse than a missing row a human can fill in by hand.
                """,
            Tools = [ReportScoreboardTool],
            ToolChoice = new ToolChoiceTool { Name = "report_scoreboard" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "Read this Rainbow Six Siege scoreboard and call report_scoreboard." },
                        new ImageBlockParam { Source = new Base64ImageSource { Data = base64, MediaType = mimeType } }
                    }
                }
            ]
        };

        var response = await _client.Messages.Create(request, ct);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }

        if (toolUse is null) return [];

        var json = JsonSerializer.Serialize(toolUse.Input);
        var parsed = JsonSerializer.Deserialize<ScoreboardResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return parsed?.Players ?? [];
    }

    private sealed record ScoreboardResult(List<ScoreboardRow> Players);
}
