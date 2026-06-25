using Anthropic;
using Anthropic.Models.Messages;
using System.Text.Json;

namespace ITMartinMusicHelper.Web.Services;

public record SongSession(
    string[] Chords,
    string StrummingPattern,
    string StrummingDescription,
    string[] MelodyTips,
    Dictionary<string, string> TransitionTips,
    string GeneralTip
);

public sealed class GuitarAiService
{
    private readonly AnthropicClient? _client;

    public GuitarAiService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<SongSession> GenerateSessionAsync(string mood)
    {
        if (_client is null) return Fallback(mood);

        var prompt = $$$"""
            A beginner guitarist wants to write a {{{mood}}} song from scratch.
            They have a guitar in their hand right now. Give them a session plan.

            Reply with ONLY valid JSON in this exact format (no markdown, no explanation):
            {{
              "chords": ["G", "D", "Em", "C"],
              "strumming": "↓ ↓ ↑ ↓ ↑",
              "strumming_description": "Tæl 1 2 og 3 og — mærk pulsen på 1 og 3",
              "melody_tips": [
                "Nyn på den 5. tone (D) over en G-akkord",
                "Lad melodien stige når spændingen bygger op",
                "Prøv 'la la la' for at finde melodien"
              ],
              "transitions": {{
                "G→D": "Behold finger 3 på B-strengen, lad kun de andre fingre glide",
                "D→Em": "Slip fingrene og skift til Em — meget let overgang",
                "Em→C": "Behold midterfingeren på B-strengen, tilføj de andre"
              }},
              "tip": "Spil langsomt — én strum per akkord til det sidder i fingrene"
            }}

            Rules:
            - Use 3-4 chords max, beginner-friendly (prefer open chords like G C D Em Am E A)
            - Strumming pattern uses ↓ and ↑ symbols only
            - melody_tips: 3 practical tips in Danish
            - transitions: one tip per chord pair in Danish
            - tip: one short general tip in Danish
            - All text in Danish
            """;

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 800,
                Messages =
                [
                    new() { Role = Role.User, Content = prompt }
                ]
            });

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var tb))
                {
                    var json = tb.Text.Trim();
                    if (json.StartsWith("```")) json = json.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```")).Aggregate((a, b) => a + "\n" + b);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var chords = root.GetProperty("chords").EnumerateArray().Select(x => x.GetString()!).ToArray();
                    var strumming = root.GetProperty("strumming").GetString() ?? "↓ ↓ ↑ ↓ ↑";
                    var strumDesc = root.GetProperty("strumming_description").GetString() ?? "";
                    var melodyTips = root.GetProperty("melody_tips").EnumerateArray().Select(x => x.GetString()!).ToArray();
                    var transitions = root.GetProperty("transitions").EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetString()!);
                    var tip = root.GetProperty("tip").GetString() ?? "";

                    return new SongSession(chords, strumming, strumDesc, melodyTips, transitions, tip);
                }
            }
        }
        catch { }

        return Fallback(mood);
    }

    public async Task<string> GetMoreTipsAsync(string[] chords, string mood)
    {
        if (_client is null) return "Øv dig på overgangene langsomt, og optag alle idéer med det samme!";

        var chordsStr = string.Join(" → ", chords);
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeHaiku4_5,
                MaxTokens = 500,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"Jeg spiller {chordsStr} til en {mood} sang. Giv mig 3 konkrete tips på dansk om: melodi, dynamik (stille/høj), og hvornår jeg skal bytte akkord. Vær kort og praktisk."
                    }
                ]
            });

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var tb)) return tb.Text.Trim();
            }
        }
        catch { }

        return "Prøv at synge melodien på 'la' mens du spiller — lad det komme naturligt.";
    }

    private static SongSession Fallback(string mood) => mood switch
    {
        "glad" => new(
            ["G", "D", "Em", "C"],
            "↓ ↓ ↑ ↓ ↑",
            "Tæl 1 2 og 3 og — mærk pulsen på 1 og 3",
            ["Nyn på D-tonen over G", "Lad melodien hoppe opad i omkvædet", "Prøv at synge 'la la la' for at finde melodien"],
            new() { ["G→D"] = "Løft kun ring- og lillefingeren fra G", ["D→Em"] = "Meget let — slip alle fingre og skub til Em", ["Em→C"] = "Behold midterfingeren, tilføj pege- og ringfingeren" },
            "Spil langsomt og nyd akkordskiftene"
        ),
        "trist" => new(
            ["Am", "F", "C", "G"],
            "↓ . ↑ . ↓ ↑",
            "Rolig og tung — ét strum på slag 1, let opstrum på og",
            ["Nyn på A-tonen over Am", "Lad melodien falde på de triste ord", "Prøv at synge lavt og inderligt"],
            new() { ["Am→F"] = "Prøv Fmaj7 — nemmere end F barre", ["F→C"] = "Let skift — to fingre går næsten samme sted", ["C→G"] = "Ring- og lillefinger glider ned til G" },
            "Langsom strum med pauser lyder meget følelsesfuld"
        ),
        _ => new(
            ["Em", "C", "G", "D"],
            "↓ ↓ ↑ ↓ ↑",
            "Tæl 1 2 og 3 og — mærk pulsen",
            ["Nyn på E-tonen over Em", "Lad melodien stræbe op mod omkvædet", "Optag alle idéer med det samme"],
            new() { ["Em→C"] = "Behold midterfingeren på plads", ["C→G"] = "To fingre glider ned", ["G→D"] = "Løft ring- og lillefingeren" },
            "Det vigtigste er at optage dine idéer med det samme"
        )
    };
}
