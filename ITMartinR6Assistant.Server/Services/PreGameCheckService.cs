using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinR6Assistant.Server.Services;

// v1 = the "2 minutes before you play" script: gather system state locally
// (PowerShell, no admin rights needed) and get back a short checklist of
// what's fine vs what to fix. v2 (not built yet) is the deeper troubleshooting
// flow for when v1's fixes don't actually solve the problem.
public sealed class PreGameCheckService
{
    private readonly AnthropicClient? _client;

    public PreGameCheckService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<string> AnalyzeAsync(string systemStateJson)
    {
        if (_client is null) return "AI ikke konfigureret (mangler Claude API-nøgle på serveren).";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 900,
            System = """
                Du er en teknisk pre-game assistent for Rainbow Six Siege-spillere.
                Du får et JSON-objekt med systemtilstand indsamlet af et lokalt script lige før en spilsession.
                Din opgave: giv en kort, konkret tjekliste - hvad er fint, og hvad skal rettes før spillet starter.

                Regler:
                - Grupper svaret i sektioner: Netværk, Lyd & Discord, Spil & Launcher, System.
                - Brug ✅ for ting der er fine, ⚠ for ting der bør tjekkes/rettes.
                - Hver ⚠-linje skal have ét konkret, praktisk forslag til at rette det - ikke bare "tjek dette".
                - Vær kortfattet - dette skal kunne læses på under et minut, ikke en rapport.
                - Hvis noget mangler i data (fx en check der fejlede lokalt), spring det roligt over i stedet for at gætte.
                - Svar KUN med tjeklisten, ingen indledning eller afsluttende kommentarer.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Systemtilstand (JSON):\n{systemStateJson}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "Kunne ikke generere tjekliste.";
    }
}
