using System.Collections.Concurrent;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinR6Assistant.Server.Services;

// A picklist of R6 Siege settings with an AI-generated explanation of what
// each one actually does and a concrete example of the tradeoff, so players
// can make an informed choice instead of guessing at menu labels. Explanations
// are cached per setting (in-memory, process lifetime) - the meaning of a
// setting doesn't change from one player asking to the next, so there's no
// reason to pay for the same explanation twice. If the process restarts, the
// cache is simply rebuilt on next request - no persistence needed for this.
public sealed class SettingsGuideService
{
    // Curated, not exhaustive - the settings most likely to actually matter for
    // competitive play or that people ask about. Exact menu wording can drift
    // between game updates; Claude is asked to explain the concept, not quote
    // Ubisoft's current menu text verbatim. Icon is just an emoji - no asset
    // management needed, matching the spirit of Magic's set-icon-grid picker
    // without depending on external image files.
    public sealed record SettingEntry(string Category, string Name, string Icon)
    {
        public string Key => $"{Category}: {Name}";
    }

    public static readonly SettingEntry[] Settings =
    [
        // Skærm & Grafik
        new("Skærm", "Opløsning (Resolution)", "🖥️"),
        new("Skærm", "Skærmtilstand (Fullscreen/Borderless/Windowed)", "🪟"),
        new("Skærm", "Opdateringshastighed (Refresh Rate)", "🔄"),
        new("Skærm", "Render Scaling (Opløsningsskalering)", "📐"),
        new("Skærm", "V-Sync", "🔃"),
        new("Skærm", "Frame Rate Limit (Billedhastighedsgrænse)", "🎞️"),
        new("Skærm", "Nvidia Reflex / Low Latency Mode", "⚡"),
        new("Skærm", "DLSS/FSR/Super Resolution (Opskalering)", "✨"),
        new("Skærm", "Skarphed (Sharpness)", "🔍"),
        new("Skærm", "Field of View - FOV", "👁️"),
        new("Skærm", "Anti-Aliasing", "🔲"),
        new("Skærm", "Anisotropic Filtering", "🎨"),
        new("Skærm", "Ambient Occlusion", "🌑"),
        new("Skærm", "Texture Quality", "🧱"),
        new("Skærm", "Shadow Quality", "🌓"),
        new("Skærm", "Reflection Quality", "🪞"),
        new("Skærm", "Motion Blur", "💨"),
        new("Skærm", "Depth of Field", "🔭"),
        new("Skærm", "Film Grain", "📽️"),
        new("Skærm", "Chromatic Aberration", "🌈"),
        new("Skærm", "Lens Effect", "🔮"),
        new("Skærm", "HDR", "🌟"),
        new("Skærm", "Lysstyrke / Gamma", "💡"),
        new("Skærm", "Farveblindtilstand (Colorblind Mode)", "👓"),

        // Sigte & Kontrol
        new("Kontrol", "Mus-følsomhed - Hip Fire", "🖱️"),
        new("Kontrol", "Mus-følsomhed - ADS (Aim Down Sight)", "🎯"),
        new("Kontrol", "ADS-følsomhed pr. sigtekorn/zoom-niveau", "🔎"),
        new("Kontrol", "Mus-acceleration", "🚀"),
        new("Kontrol", "Raw Input", "🔌"),
        new("Kontrol", "Deadzone (Controller)", "🕹️"),
        new("Kontrol", "Aim Response Curve (Linear vs. Exponential)", "📈"),
        new("Kontrol", "Controller-vibration", "📳"),
        new("Kontrol", "Toggle vs. Hold (Crouch/Prone/Lean/ADS)", "🔘"),
        new("Kontrol", "Lean-følsomhed", "↔️"),
        new("Kontrol", "Y-akse invertering", "↕️"),

        // Lyd
        new("Lyd", "Master Volume", "🔊"),
        new("Lyd", "Musik-volume", "🎵"),
        new("Lyd", "Lydeffekt-volume", "💥"),
        new("Lyd", "Voice Chat Volume", "🎙️"),
        new("Lyd", "Push-to-Talk vs. Voice Activation", "📢"),
        new("Lyd", "HDR Audio / 3D Positional Audio", "🎧"),
        new("Lyd", "Mono Audio", "🔈"),

        // Discord (mikrofon/lyd - ikke R6's egne indstillinger)
        new("Discord", "Input/Output Device (valg af mikrofon/højttaler)", "🎤"),
        new("Discord", "Input Sensitivity (Automatisk vs. manuel tærskel)", "📶"),
        new("Discord", "Automatic Gain Control", "🎚️"),
        new("Discord", "Echo Cancellation", "🔁"),
        new("Discord", "Noise Suppression / Krisp", "🧹"),
        new("Discord", "Advanced Voice Activity", "🌊"),
        new("Discord", "Audio Subsystem (Standard vs. Legacy)", "🔧"),
        new("Discord", "Push to Talk - tastebinding og release-delay", "⌨️"),

        // System
        new("System", "Renderer (DirectX vs. Vulkan)", "⚙️"),
    ];

    private readonly AnthropicClient? _client;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public SettingsGuideService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<string> ExplainAsync(string setting)
    {
        if (_client is null) return "AI ikke konfigureret (mangler Claude API-nøgle på serveren).";
        if (_cache.TryGetValue(setting, out var cached)) return cached;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 500,
            System = """
                Du forklarer en enkelt indstilling fra Rainbow Six Siege's indstillingsmenu til en spiller,
                der overvejer at ændre den.

                Svar i PRÆCIS dette format, intet andet:
                HVAD DET GØR: [1-2 sætninger, ren forklaring uden fagjargon]
                SKRUER DU OP: [konkret eksempel på konsekvensen ved højere/til]
                SKRUER DU NED: [konkret eksempel på konsekvensen ved lavere/fra]
                I PRAKSIS: [et konkret scenarie fra en faktisk kamp - fx "du peeker en gang og skal se en fjende der crouker bag en sandsæk" eller "du hører fodtrin på 2. sal og skal vurdere retning" - hvor denne indstilling reelt gør en forskel]
                ANBEFALING TIL KONKURRENCE-SPIL: [1 kort, konkret anbefaling til nogen der spiller for at vinde, ikke for grafikken]

                Vær konkret og praktisk - "giver bedre FPS men mister detaljer i skygger" er godt,
                "påvirker performance" er ikke godt nok. "I PRAKSIS" skal være et virkeligt spilmoment,
                ikke en gentagelse af de to linjer ovenfor. Hvis indstillingen ikke er relevant for
                konkurrenceniveau (fx rent kosmetisk), sig det ærligt i anbefalingen.

                Skriv naturligt dansk, som en dansk gamer faktisk ville sige det til en ven - ikke en
                direkte oversættelse fra engelsk. Undgå typiske oversættelses-anglicismer og engelsk
                sætningsopbygning ("dette vil resultere i" -> "det giver"; "det er vigtigt at bemærke at"
                -> bare drop det; "performance" -> "ydelse/billedhastighed" medmindre det er et fastlåst
                låneord blandt danske spillere). Selve indstillingsnavnet må gerne beholde det engelske
                UI-udtryk (fx "V-Sync"), men forklaringen omkring det skal lyde som almindeligt talt dansk.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Forklar denne indstilling i Rainbow Six Siege: {setting}"
                }
            ]
        });

        var text = "Kunne ikke generere forklaring.";
        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) { text = tb.Text.Trim(); break; }

        _cache[setting] = text;
        return text;
    }
}
