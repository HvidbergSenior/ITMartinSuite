namespace ITMartinMusikStudio.Server.Services;

// A library of progressions worth stealing, written as scale degrees so they
// can be shown in whatever key the song is in. Degrees: 1-7, with "b"/"#"
// prefixes, and a quality suffix ("m", "7", "maj7", "sus4", "dim"...). A slash
// gives the bass degree ("5/7" = V with the 7th scale degree in the bass, G/B
// in C). "Verse:"-style section names are kept so "Brug" drops straight into
// the chord chart in the format the studio already uses.
public sealed record Progression(string Name, string Mood, bool Minor, string Degrees, string Note = "")
{
    // Sections separated by " | ", degrees by spaces, e.g. "Verse: 1 5 6m 4 | Chorus: 4 5 1"
}

public static class ChordProgressions
{
    public static readonly IReadOnlyList<Progression> All =
    [
        // ── Major-key classics ─────────────────────────────────────────
        new("Popsangen", "Glad, kendt fra tusind hits", false, "Vers: 1 5 6m 4 | Omkvæd: 1 5 6m 4", "I–V–vi–IV. Let It Be, No Woman No Cry, Someone Like You."),
        new("Firserballaden", "Lys, længselsfuld", false, "Vers: 6m 4 1 5 | Omkvæd: 6m 4 1 5", "vi–IV–I–V. Samme akkorder som popsangen, men starter på mol."),
        new("Doo-wop / 50'er", "Sød, nostalgisk", false, "Vers: 1 6m 4 5 | Omkvæd: 1 6m 4 5", "I–vi–IV–V. Stand By Me, Every Breath You Take."),
        new("Folk / country", "Ærlig, enkel", false, "Vers: 1 4 1 5 | Omkvæd: 4 1 5 1", "Tre akkorder er nok. Ring of Fire, Country Roads."),
        new("Nedadgående bas", "Blød, elegant ballade", false, "Intro: 1 5/7 6m7 4maj7 | Vers: 1 5/7 6m7 4maj7 2m7 5sus4 5 1", "Bassen går C–B–A–F–D–G–C. Behold ringfingeren på D-strengen 3. bånd gennem de første fire."),
        new("Canon (Pachelbel)", "Højtidelig, rund", false, "Vers: 1 5 6m 3m 4 1 4 5", "Basket Case, Go West, Don't Look Back in Anger."),
        new("Bluesen (12 takter)", "Rå, groovy", false, "Vers: 17 17 17 17 47 47 17 17 57 47 17 57", "Alle akkorder som dominant-7. Hver akkord = én takt."),
        new("Gospel / soul", "Varm, løftet", false, "Vers: 1 3m 4 5 | Omkvæd: 6m 2m 5 1", "Tremolo-strum og en løftet stemme."),
        new("Jazz-turnaround", "Sofistikeret", false, "Vers: 1maj7 6m7 2m7 57 | Omkvæd: 3m7 6m7 2m7 57", "I–vi–ii–V. Fly Me to the Moon, Blue Moon."),
        new("Fest / rock", "Energi, køre-hurtigt", false, "Vers: 1 4 5 4 | Omkvæd: 1 4 5 4", "Wild Thing, Louie Louie, La Bamba."),
        new("Mixolydisk rock", "Stor, åben", false, "Vers: 1 b7 4 1 | Omkvæd: 1 b7 4 1", "Sweet Child o' Mine, Sweet Home Alabama (bVII giver rock-lyden)."),
        new("Sentimental bro", "Skift af farve midt i sangen", false, "Bro: 4 4m 1 5 | Bro 2: 2m 4m 1", "Den mol-firer (iv) er tricket - Creep, Something."),
        new("C–G–D–F (lydisk pop)", "Lys, overraskende", false, "Vers: 1 5 2 4 | Omkvæd: 1 5 2 4", "I–V–II–IV. Dur-toeren (D i C-dur) giver det lyse, lidt uventede løft. Ret trin 2 til 2m for den almindelige version."),
        new("Lullaby / vuggevise", "Rolig, gyngende", false, "Vers: 1 4 1 5 | Omkvæd: 1 4 5 1", "Fingerspil, langsomt, gerne med capo."),
        new("Beatles-vending", "Klassisk, rund afslutning", false, "Vers: 1 4 5 1 | Omkvæd: 2m 5 1 1", "ii–V–I som landing. Hey Jude, Let It Be-omkvæd."),
        new("Creep-vendingen", "Bittersød", false, "Vers: 1 3 4 4m | Omkvæd: 1 3 4 4m", "I–III–IV–iv. Dur-treeren og mol-fireren gør det. Creep, Space Oddity-stemning."),
        new("Visesang (dansk)", "Enkel, fortællende", false, "Vers: 1 5 1 4 1 5 1 | Omkvæd: 4 1 5 1", "Højskolesangbogen. Tre akkorder, teksten bærer."),
        new("Country-vals (3/4)", "Gyngende, varm", false, "Vers: 1 1 4 1 5 5 1 1 | Omkvæd: 4 4 1 1 5 5 1 1", "Tæl 1-2-3. Én akkord pr. takt. Bas på 1, strum på 2 og 3."),
        new("Reggae / roots", "Afslappet", false, "Vers: 1 5 6m 4 | Omkvæd: 4 5 1 1", "Skank på 2 og 4 (dæmp mellem). Three Little Birds er kun I–IV–V."),

        // ── Minor-key ──────────────────────────────────────────────────
        new("Melankolsk pop", "Vemodig, drivende", true, "Vers: 1m b6 b3 b7 | Omkvæd: 1m b6 b3 b7", "i–VI–III–VII. Numb, Zombie, Save Tonight, Du Sagde Ingenting-stemning."),
        new("Andalusisk (flamenco)", "Dramatisk, nedadgående", true, "Vers: 1m b7 b6 5 | Omkvæd: 1m b7 b6 5", "Hit the Road Jack, Sultans of Swing. Bassen falder trin for trin."),
        new("Molballaden", "Tung, ærlig", true, "Vers: 1m 4m 5 1m | Omkvæd: b6 b7 1m 1m", "Klassisk mol med dominant (dur-femmer) tilbage til hjem."),
        new("Mol med lys i enden", "Mørk vers, lyst omkvæd", true, "Vers: 1m b7 b6 b7 | Omkvæd: b3 b7 1m b6", "Vers i mol, omkvæd åbner sig i relativ dur (bIII)."),
        new("Rolig folk-mol", "Nordisk, stille", true, "Vers: 1m b3 b7 4m | Omkvæd: b6 b3 b7 1m", "Fingerspil. Lyder godt med capo 2-4."),
        new("Wicked Game", "Svævende, tre akkorder", true, "Vers: 1m b7 4 | Omkvæd: 1m b7 4", "i–VII–IV. Bm–A–E i original. Skift på takt 1, lad dem klinge."),
        new("Mol-blues", "Sjælfuld", true, "Vers: 1m7 1m7 1m7 1m7 4m7 4m7 1m7 1m7 b67 57 1m7 57", "12 takter i mol - Thrill Is Gone, Ain't No Sunshine-stemning."),
        new("Faldende mol-bas (Stairway)", "Sørgmodig, klassisk", true, "Vers: 1m 1m/7 1m/b7 1m/6 | Omkvæd: b6 5 1m 1m", "Am–Am/G#–Am/G–Am/F#. Stairway, Michelle, Chim Chim Cheree. Kun bassen flytter sig."),
        new("Mad World", "Tom, smuk", true, "Vers: 1m b3 b7 4 | Omkvæd: 1m b3 b7 4", "i–III–VII–IV. Den dur-firer (IV i mol) er lyset i mørket."),
        new("House of the Rising Sun", "Folk-drama i 6/8", true, "Vers: 1m b3 4 b6 | Omkvæd: 1m b3 5 5", "Arpeggio i 6/8. Am C D F / Am C E E."),
        new("Mol-pop med dur-omkvæd", "Løfter sig", true, "Vers: 1m b7 b6 b7 | Omkvæd: b3 5 1m b6", "Vers dvæler; omkvædet starter i relativ dur og slutter hjemme."),
    ];

    private static readonly string[] Sharps = ["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"];
    private static readonly string[] Flats  = ["C","Db","D","Eb","E","F","Gb","G","Ab","A","Bb","B"];
    private static readonly int[] MajorSteps = [0, 2, 4, 5, 7, 9, 11];

    // "F#m" -> (6, true), "Bb" -> (10, false). Unknown -> C major.
    public static (int Root, bool Minor) ParseKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return (0, false);
        var k = key.Trim();
        var minor = k.EndsWith("m", StringComparison.Ordinal) && !k.EndsWith("maj", StringComparison.Ordinal);
        var root = minor ? k[..^1] : k;
        var idx = Array.IndexOf(Sharps, root);
        if (idx < 0) idx = Array.IndexOf(Flats, root);
        if (idx < 0) idx = Array.IndexOf(Sharps, root.Length > 0 ? root[..1] : "C");
        return (Math.Max(0, idx), minor);
    }

    public static bool KeyPrefersFlats(int root, bool minor) =>
        minor ? root is 5 or 10 or 3 or 8 or 1 : root is 5 or 10 or 3 or 8 or 1;

    // Renders "Vers: 1 5/7 6m7 4maj7" in the given key -> "Vers: C G/B Am7 Fmaj7".
    public static string Render(Progression p, int root, bool minor)
    {
        var useFlats = KeyPrefersFlats(root, minor);
        var sections = p.Degrees.Split('|', StringSplitOptions.TrimEntries);
        var lines = new List<string>();
        foreach (var sec in sections)
        {
            var colon = sec.IndexOf(':');
            var label = colon > 0 ? sec[..colon].Trim() : "";
            var degrees = (colon > 0 ? sec[(colon + 1)..] : sec).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chords = degrees.Select(d => Degree(d, root, minor, useFlats));
            lines.Add((label.Length > 0 ? label + ": " : "") + string.Join(" ", chords));
        }
        return string.Join("\n", lines);
    }

    // "b6", "5/7", "17", "4maj7", "2m7" -> chord name in key.
    private static string Degree(string token, int root, bool minor, bool useFlats)
    {
        var bass = "";
        var slash = token.IndexOf('/');
        if (slash > 0) { bass = "/" + Degree(token[(slash + 1)..], root, minor, useFlats); token = token[..slash]; }

        var i = 0; var accidental = 0;
        while (i < token.Length && token[i] is 'b' or '#') { accidental += token[i] == 'b' ? -1 : 1; i++; }
        if (i >= token.Length || !char.IsDigit(token[i])) return token + bass;
        var degree = token[i] - '1'; i++;
        var quality = token[i..];
        if (degree is < 0 or > 6) return token + bass;

        // Degrees are always relative to the parallel MAJOR scale (Roman-numeral
        // convention): in A minor "b6" is F, "5" is E major, "4m" is Dm.
        var note = (root + MajorSteps[degree] + accidental + 12) % 12;
        return (useFlats ? Flats : Sharps)[note] + quality + bass;
    }
}

public static class EasyChords
{
    // "Am7" -> "Am", "Fmaj7" -> "F", "G/B" -> "G", "Gsus4" -> "G", "Bdim" -> "Bm",
    // "C#m7b5" -> "C#m". Plain open triads a beginner can grab.
    public static string Simplify(string chord)
    {
        if (string.IsNullOrWhiteSpace(chord)) return chord;
        var top = chord.Split('/')[0];
        var len = top.Length > 1 && top[1] is '#' or 'b' ? 2 : 1;
        if (top.Length < len) return chord;
        var root = top[..len];
        var rest = top[len..];
        var minor = rest.StartsWith('m') && !rest.StartsWith("maj") || rest.StartsWith("dim") || rest.Contains("m7b5");
        return root + (minor ? "m" : "");
    }

    // Whole chart, line by line, labels kept.
    public static string SimplifyChart(string chart) =>
        string.Join("\n", chart.Split('\n').Select(line =>
        {
            var c = line.IndexOf(':');
            var label = c > 0 ? line[..(c + 1)] + " " : "";
            var chords = (c > 0 ? line[(c + 1)..] : line).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Simplify);
            return label + string.Join(" ", chords);
        }));
}
