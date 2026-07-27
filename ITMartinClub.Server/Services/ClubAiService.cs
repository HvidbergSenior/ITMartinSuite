using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinClub.Server.Services;

// Turns a pile of quick one-liner notes from multiple players into a longer,
// funny, embellished recap of the game session. One call per recap request -
// explicitly on-demand (a button someone presses after playing), never
// automatic, and uses the cheapest suitable model.
public sealed class ClubAiService
{
    private readonly AnthropicClient? _client;

    public ClubAiService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    // statsSummary is an optional pre-formatted block (K/D leaderboard, bomb
    // success rate, lone-survivor call-outs) computed by the caller from that
    // evening's Match/MatchPlayerStat rows - passed as text rather than typed
    // objects to keep this service decoupled from the stats data model.
    public async Task<string> GenerateRecapAsync(List<(string MemberName, string Text)> notes, string? statsSummary = null)
    {
        if (_client is null || (notes.Count == 0 && string.IsNullOrWhiteSpace(statsSummary))) return "";

        var playerNames = notes.Select(n => n.MemberName).Distinct().ToList();
        var notesText = notes.Count == 0 ? "(ingen noter denne aften)" : string.Join("\n", notes.Select(n => $"- {n.MemberName}: {n.Text}"));
        var statsText = string.IsNullOrWhiteSpace(statsSummary) ? "(ingen kampstatistik registreret)" : statsSummary;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1500,
            System = $"""
                Du skriver en sjov, fyldig kamprapport/resumé af en Rainbow Six Siege-aften for en gruppe
                venner der kender hinanden godt og elsker sort/skarp humor - de tager IKKE stødig humor
                ilde op, og roasting er en del af kulturen. Svar på dansk.

                Input er to ting: (1) korte noter fra hver spiller om aftenen, deres eget perspektiv - du
                må frit digte videre på stemningen her, og (2) faktiske kamptal (K/D, bombe-plant rate,
                lone survivor). Når rigtige tal er givet, brug dem konkret i teksten (fx faktisk K/D) i
                stedet for kun at gætte på stemningen - det gør historien sjovere og mere præcis på samme tid.

                REGLER, følg dem PRÆCIST:
                1. Hvis der er spillernavne i noterne, skal ALLE nævnes ved navn mindst én gang:
                   {(playerNames.Count > 0 ? string.Join(", ", playerNames) : "(ingen fra noterne)")}. Ingen må mangle.
                2. Skriv langt og fyldigt - dette er en underholdende historie/beretning, ikke et kort
                   resumé. Byg stemning, brug gerne humoristiske sammenligninger og overdrivelser.
                3. Du må roaste hårdt for dårlige spil, dumme døde, dårlige beslutninger - det er okay,
                   gruppen kan tåle det og forventer det.
                4. MEN: hver eneste spiller skal OGSÅ have mindst ét ægte positivt highlight i teksten,
                   uanset hvor dårligt de ellers spillede. Det behøver ikke være stort - "han fik da
                   klaret et par stykker" eller "trak i det mindste opmærksomhed fra resten af holdet"
                   tæller fint. Ingen spiller må fremstå som ren komisk skydeskive uden et eneste
                   lyspunkt.
                5. Skriv som en engageret, morsom fortæller - ikke en tør statistikliste.

                Svar KUN med selve teksten, ingen overskrift eller meta-kommentar før/efter.
                """,
            Messages =
            [
                new() { Role = Role.User, Content = $"Kampstatistik fra aftenen:\n{statsText}\n\nNoter fra aftenen:\n{notesText}" }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();

        return "";
    }

    // The constructive counterpart to GenerateRecapAsync - same input, but asks
    // for what to actually practice instead of a funny story. Kept separate
    // (not a flag on the same prompt) since the two want very different tones
    // and roughly opposite lengths.
    public async Task<string> GeneratePracticeRecapAsync(List<(string MemberName, string Text)> notes, string? statsSummary = null)
    {
        if (_client is null || (notes.Count == 0 && string.IsNullOrWhiteSpace(statsSummary))) return "";

        var notesText = notes.Count == 0 ? "(ingen noter denne aften)" : string.Join("\n", notes.Select(n => $"- {n.MemberName}: {n.Text}"));
        var statsText = string.IsNullOrWhiteSpace(statsSummary) ? "(ingen kampstatistik registreret)" : statsSummary;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du opsummerer en Rainbow Six Siege-aften for en gruppe venner, men modsat den sjove
                version skal denne være KORT og KONSTRUKTIV: hvad gik godt, og - vigtigst - hvad kunne
                gruppen øve eller gøre anderledes til næste gang. Ingen roasting, ingen jokes. Tænk
                "kort trænerfeedback", ikke "morsom historie".

                Brug de faktiske kamptal hvis de er givet (K/D, bombe-rate) til at pege på noget konkret
                (fx "bombe lykkedes kun X% af gangene - værd at øve plant-timing"). Hvis notérne nævner
                noget der gik galt taktisk, foreslå kort hvordan det kan forbedres. Maks 4-6 korte linjer
                i alt, gerne som en kort punktopstilling. Svar på dansk. Svar KUN med selve teksten, ingen
                overskrift.
                """,
            Messages =
            [
                new() { Role = Role.User, Content = $"Kampstatistik fra aftenen:\n{statsText}\n\nNoter fra aftenen:\n{notesText}" }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();

        return "";
    }

    // Short, cheap, one-line call (not the long recap) - a playful variation on
    // "is anyone ready to play" instead of the same static phrase every time.
    public async Task<string?> GenerateReadyCheckPhraseAsync(string name, int minutes)
    {
        if (_client is null) return null;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 60,
            System = """
                Du digter én kort, sjov, uformel dansk sætning der fortæller at en spiller er klar til at
                spille Rainbow Six Siege om et stykke tid, og spørger om andre også er klar - i stil med
                "Skal vi PANG-PANG sammen om 10 minutter?" eller "Vil nogen skyde nogen med mig om en time?".
                Brug spillerens navn og tidsangivelsen naturligt i sætningen. Maks én sætning, gerne drilsk
                gamer-humor, men ikke stødende. Svar KUN med selve sætningen - ingen anførselstegn, ingen
                forklaring.
                """,
            Messages =
            [
                new() { Role = Role.User, Content = $"Navn: {name}. Tid: {FormatMinutes(minutes)}." }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();

        return null;
    }

    private static string FormatMinutes(int minutes) =>
        minutes % 60 == 0 && minutes >= 60 ? $"{minutes / 60} time(r)" : $"{minutes} minutter";
}
