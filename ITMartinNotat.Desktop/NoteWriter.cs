using System.IO;
using System.Security.Cryptography;
using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace ITMartinNotat;

/// <summary>Turns (already pseudonymised) keywords into readable Danish journal text with Claude.</summary>
public static class NoteWriter
{
    public const string Model = "claude-opus-5-5";

    public static readonly string[] Styles =
    [
        "Journalnotat – løbende tekst",
        "Journalnotat med overskrifter (Subjektivt / Objektivt / Vurdering / Plan)",
        "Kort resumé (3-5 linjer)",
        "Punktform – rettet til hele sætninger",
    ];

    private const string SystemPrompt = """
        Du hjælper en dansk sundhedsfaglig medarbejder med at gøre hendes stikord til læsbar journaltekst.

        Stikordene er hendes egne observationer. Personoplysninger er erstattet med pladsholdere som [NAVN1], [CPR1],
        [DATO2], [TELEFON1], [ADRESSE1], [EMAIL1] og [POSTNR1]. De bliver sat tilbage, efter du har skrevet teksten.

        Regler:
        - Brug kun det, der står i stikordene. Tilføj aldrig fund, diagnoser, målinger, doser, årsager eller aftaler,
          som ikke står der – heller ikke selvom de virker sandsynlige. Hellere en kort tekst end en opdigtet detalje.
        - Bevar alle pladsholdere præcis som de står (med firkantede parenteser og nummer) – oversæt eller fjern dem aldrig.
        - Bevar tal, enheder, medicinnavne og doser nøjagtigt som skrevet.
        - Hvis et stikord er uklart eller kan læses på flere måder, så skriv det så tæt på originalen som muligt og sæt [?] efter.
        - Skriv neutralt, sagligt og kort, som i en dansk patientjournal: hele sætninger, nutid eller datid som stikordene lægger op til,
          ingen vurderende eller pyntende ord, ingen gentagelser.
        - Skriv kun selve notatet. Ingen indledning, forklaring eller afslutning, ingen markdown-stjerner eller #.
        """;

    public static async Task<string> WriteAsync(string apiKey, string keywords, string style, string extra, CancellationToken ct)
    {
        var client = new AnthropicClient { ApiKey = apiKey };
        var user = new StringBuilder()
            .AppendLine($"Form: {style}")
            .AppendLine(string.IsNullOrWhiteSpace(extra) ? "" : $"Ekstra ønske: {extra.Trim()}")
            .AppendLine("Stikord:")
            .AppendLine("<stikord>")
            .AppendLine(keywords.Trim())
            .AppendLine("</stikord>")
            .ToString();

        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                System = SystemPrompt,
                Messages = [new() { Role = Role.User, Content = user }],
            }, ct);

            if (response.StopReason == "refusal")
                throw new InvalidOperationException("AI'en ville ikke skrive dette notat. Prøv at omformulere stikordene.");
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
            if (response.StopReason == "max_tokens")
                text += "\n\n[Notatet blev for langt og er klippet af her]";
            return text;
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new InvalidOperationException("API-nøglen virker ikke. Tryk ⚙ Indstillinger og indsæt en ny.");
        }
        catch (AnthropicRateLimitException)
        {
            throw new InvalidOperationException("For mange forespørgsler lige nu. Vent et minut og prøv igen.");
        }
        catch (AnthropicApiException e)
        {
            throw new InvalidOperationException($"AI-tjenesten svarede med en fejl: {e.Message}");
        }
        catch (System.Net.Http.HttpRequestException)
        {
            throw new InvalidOperationException("Ingen forbindelse til AI-tjenesten. Tjek internettet.");
        }
    }
}

/// <summary>Which AI to use. Local is the default: the cloud only after the workplace has approved it.</summary>
public static class AppMode
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notatskriver", "tilstand.txt");

    public static bool Cloud
    {
        get { try { return File.Exists(FilePath) && File.ReadAllText(FilePath).Trim() == "sky"; } catch { return false; } }
        set { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, value ? "sky" : "lokal"); }
    }
}

/// <summary>The API key, encrypted with Windows (DPAPI) so only this Windows user can read it. Nothing else is stored.</summary>
public static class KeyStore
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notatskriver");
    private static readonly string FilePath = Path.Combine(Dir, "nøgle.bin");

    public static string? Load()
    {
        var env = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        try
        {
            if (!File.Exists(FilePath)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }

    public static void Save(string key)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
    }
}
