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
                - Hvis r6_dage_siden_opdateret er højt (fx over 14 dage), er det TOP PRIORITET: gør klart at der
                  sandsynligvis venter en stor opdatering, og at Ubisoft Connect bør åbnes NU (ikke lige før kickoff)
                  så downloadet er færdigt til tiden - store patches efter en lang pause kan tage lang tid.
                - Hvis dage_siden_genstart er højt (fx over 14 dage), foreslå en genstart inden spillet - en hurtig,
                  gratis fix for fastlåste opdateringer eller driver-tilstand efter lang tids inaktivitet.
                - headset_software er en liste af kendt lyd-software der kører (fx SteelSeries GG/Sonar, Logitech
                  G HUB, Razer Synapse, Corsair iCUE, HyperX NGENUITY). En TOM liste er det NORMALE og HELT FINE
                  for de fleste - de fleste headsets kræver ingen ekstra software overhovedet. En tom liste er
                  IKKE noget der mangler at blive konfigureret, og er ALDRIG en ⚠-linje - opfind ikke en advarsel
                  om at "headset-software ikke er konfigureret". Nævn kun noget om headset-software i tjeklisten
                  hvis listen rent faktisk indeholder ét eller flere navne - og så kun som info om at det programs
                  egne lyd-indstillinger (støjfjernelse/EQ/enhedsvalg) også er værd at tjekke, hvis lyden driller.
                - nylige_system_fejl og nylige_app_fejl er uddrag fra Windows' egen Hændelseslog fra de sidste 3
                  dage (WHEA/GPU-driver-events hhv. R6-relaterede crashes). Er en af dem IKKE tom, er det TOP
                  PRIORITET at nævne det under System: forklar kort hvad det betyder (fx en WHEA-fejl = et
                  hardware-niveau problem, ofte PCIe/GPU/strøm-relateret - foreslå at tjekke GPU-kablet/PSU-kablet
                  sidder ordentligt, eller opdatere/rulle GPU-driveren tilbage), og at det kan forklare stutter/
                  freeze/crash under spillet, ikke kun noget der sker "ved siden af" spillet.
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

    // v2's actual starting point: on-demand help for "X isn't working right
    // now", using whatever setup info is already known about the player
    // (their specs + their last PreGameCheck submission, if any) so the
    // advice can be specific to their actual hardware/software instead of
    // generic ("check your Discord settings" vs "your SteelSeries Sonar's
    // noise suppression can cut out mid-sentence - try turning it down").
    public async Task<string> AskForHelpAsync(string problem, string knownSetupJson)
    {
        if (_client is null) return "AI ikke konfigureret (mangler Claude API-nøgle på serveren).";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 700,
            System = """
                Du er en teknisk assistent for Rainbow Six Siege-spillere. En spiller beskriver et konkret problem
                lige nu (fx "min lyd virker ikke"). Du får også et JSON-objekt med det der er kendt om spillerens
                udstyr/setup (specs og/eller seneste pre-game-tjek) - brug det til at gøre svaret specifikt for
                DERES udstyr, ikke en generisk guide.

                Regler:
                - Giv 3-5 konkrete, ordnede trin - det mest sandsynlige fix først.
                - Er der en kendt headset-model eller headset-software i data, nævn den model/det programs
                  konkrete indstilling (fx "åbn SteelSeries Sonar → Chat-mixer → tjek at Discord ikke er mutet der"),
                  ikke bare "tjek din lydsoftware".
                - Hvis udstyret slet ikke er kendt (tomt/mangler i data), giv almindelige gode råd i stedet -
                  sig det ikke som en fejl, bare tilpas rådene til at være mere generelle.
                - Er der data under nylige_system_fejl eller nylige_app_fejl (uddrag fra Windows' Hændelseslog),
                  så tjek dem for noget relevant til problemet - en WHEA/GPU-driver-fejl eller et R6-crash i den
                  seneste log er ofte den faktiske årsag bag stutter/lag/freeze/lyd der dropper ud, og bør nævnes
                  som det mest sandsynlige første trin hvis den findes, frem for generelle gæt.
                - Kortfattet - dette skal kunne følges med det samme, ikke læses som en artikel.
                - Svar KUN med trinene, ingen indledning eller afsluttende kommentarer.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Problem: {problem}\n\nKendt setup (JSON):\n{knownSetupJson}"
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "Kunne ikke generere svar.";
    }
}
