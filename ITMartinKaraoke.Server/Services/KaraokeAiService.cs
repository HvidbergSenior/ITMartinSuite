using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinKaraoke.Server.Services;

// Short, practical AI-generated hints for a karaoke performance - aimed at
// making it a group thing, not a solo vocal test: how the lead singer can
// approach it, plus concrete ways for everyone else in the room to join in
// (clapping, call-and-response, a simple harmony line, drumming along on
// whatever's at hand) without needing to actually sing lead.
public sealed class KaraokeAiService
{
    private readonly AnthropicClient? _client;

    public KaraokeAiService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<string> GetSingAlongTipsAsync(string title, string artist, string plainLyrics)
    {
        if (_client is null) return "";

        var lyricsHint = string.IsNullOrWhiteSpace(plainLyrics)
            ? ""
            : $"\n\nFørste linjer af teksten:\n{string.Join("\n", plainLyrics.Split('\n').Take(10))}";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du er en hyggelig sanglærer til en karaokeaften med familie/venner - IKKE en professionel
                stemmecoach. Svar ALTID på dansk, kort og konkret, ingen generiske råd.
                Formatér PRÆCIST sådan (ingen anden tekst før eller efter):
                SOLIST: [1-2 sætninger: toneleje/register at synge i, ét sted man typisk skal trække vejret,
                og ét sted hvor sangen typisk går op eller ned i styrke]
                ALLE ANDRE: [2-3 konkrete forslag til hvordan resten af selskabet kan være med UDEN at synge
                forvers - fx synge omkvædet sammen, klappe på bestemte steder, en simpel "oh-oh" harmoni,
                eller tromme rytmen på hvad der nu er ved hånden]
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Sang: \"{title}\"{(string.IsNullOrWhiteSpace(artist) ? "" : $" af {artist}")}{lyricsHint}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb))
                return tb.Text.Trim();

        return "";
    }
}
