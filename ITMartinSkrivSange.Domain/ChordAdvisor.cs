namespace ITMartinSkrivSange.Domain;

/// <param name="Why">A few words on how it sounds, shown on the button.</param>
public sealed record ChordIdea(string Chord, string Why);

// Ideas for one chord in a part (user 2026-10-14: "suggest alternatives to the chord ... and the next chords that could sound nice").
// Plain music theory, no AI: everything stays in the song's key and in open grips.
//  - Alternatives: the same chord with colour (Em -> Em7), or a chord in the key that shares two notes with it (Em -> G).
//  - Next: where the chord likes to go in the key (V wants home to I, IV lifts, vi goes soft ...).
public static class ChordAdvisor
{
    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>The notes (0 = C ... 11 = B) in a grip.</summary>
    public static IReadOnlySet<int> Notes(string chord)
    {
        if (Chords.Split(chord) is not { } s || Chords.RootIndex(chord) is not { } root) return new HashSet<int>();
        int[] steps = s.Suffix switch
        {
            "m" => [0, 3, 7],
            "7" => [0, 4, 7, 10],
            "maj7" => [0, 4, 7, 11],
            "m7" => [0, 3, 7, 10],
            "add9" => [0, 2, 4, 7],
            "sus4" => [0, 5, 7],
            "sus2" => [0, 2, 7],
            _ => [0, 4, 7],
        };
        return steps.Select(x => (root + x) % 12).ToHashSet();
    }

    public static IReadOnlyList<ChordIdea> Alternatives(string chord, string? homeGrip)
    {
        var scale = Scale(homeGrip ?? chord);
        var notes = Notes(chord);
        var root = Chords.RootIndex(chord);
        var ideas = new List<ChordIdea>();

        // Same root, other colour - the easiest swap, often one finger.
        foreach (var g in Chords.OpenGrips.Where(g => g != chord && Chords.RootIndex(g) == root && InKey(g, scale)))
            ideas.Add(new(g, Colour(chord, g)));

        // Another chord in the key with two notes in common - same job in the song, another colour.
        foreach (var g in Chords.OpenGrips.Where(g => Chords.RootIndex(g) != root && InKey(g, scale) && IsPlain(g))
                     .Where(g => Notes(g).Intersect(notes).Count() >= 2))
            ideas.Add(new(g, Chords.IsMinor(g) == Chords.IsMinor(chord) ? $"deler to toner med {chord}" : Chords.IsMinor(g) ? $"deler to toner - mol, blødere" : "deler to toner - dur, lysere"));

        return ideas;
    }

    public static IReadOnlyList<ChordIdea> Next(string chord, string? homeGrip)
    {
        var home = homeGrip ?? chord;
        var family = Chords.RootIndex(Chords.MajorFamily(home)) ?? 0;
        var scale = Scale(home);
        var degree = Chords.RootIndex(chord) is { } r ? Array.IndexOf(MajorScale, ((r - family) % 12 + 12) % 12) : -1;

        // Steps of the major scale (0 = I, 1 = ii ... 5 = vi) and where each likes to go.
        (int To, string Why)[] moves = degree switch
        {
            0 => [(3, "løfter sig"), (4, "spænding"), (5, "blødt, mol")],
            1 => [(4, "vil videre hjem"), (3, "bløder op"), (0, "hjem")],
            2 => [(5, "glider ned i mol"), (3, "løfter sig")],
            3 => [(4, "spænding"), (0, "hjem igen"), (1, "blødt")],
            4 => [(0, "lander hjemme"), (5, "overraskende, mol"), (3, "trækker tilbage")],
            5 => [(3, "løfter sig"), (1, "blødt videre"), (4, "spænding")],
            _ => [(0, "hjem"), (4, "spænding"), (3, "løfter sig")],
        };

        var ideas = new List<ChordIdea>();
        foreach (var (to, why) in moves)
        {
            if (DegreeGrip(to, family, scale) is not { } g || g == chord) continue;
            var homeNote = Chords.IsMinor(home) && to == 5 ? " (hjem i mol)" : "";
            ideas.Add(new(g, why + homeNote));
        }
        // A minor-key song goes home to its minor chord.
        if (Chords.IsMinor(home) && chord != home && Chords.IsOpenGrip(home) && ideas.All(i => i.Chord != home))
            ideas.Add(new(home, "hjem"));
        return ideas;
    }

    // The open grip for a step of the scale: the plain chord if it is an open grip, else a coloured one with all notes in key
    // (F is a barre - Fmaj7 is not).
    private static string? DegreeGrip(int degree, int family, IReadOnlySet<int> scale)
    {
        var root = (family + MajorScale[degree]) % 12;
        var minor = degree is 1 or 2 or 5;
        return Chords.OpenGrips
            .Where(g => Chords.RootIndex(g) == root && Chords.IsMinor(g) == minor && InKey(g, scale))
            .OrderBy(g => IsPlain(g) ? 0 : 1)
            .FirstOrDefault();
    }

    private static IReadOnlySet<int> Scale(string home)
    {
        var family = Chords.RootIndex(Chords.MajorFamily(home)) ?? 0;
        return MajorScale.Select(x => (family + x) % 12).ToHashSet();
    }

    private static bool InKey(string grip, IReadOnlySet<int> scale) => Notes(grip).All(scale.Contains);

    private static bool IsPlain(string grip) => Chords.Split(grip) is { } s && s.Suffix is "" or "m";

    private static string Colour(string from, string to) => Chords.Split(to)!.Value.Suffix switch
    {
        "7" => "mere bluesy, vil videre",
        "maj7" => "drømmende",
        "m7" => "blødere",
        "add9" => "luftig",
        "sus4" => "svævende, vil løses",
        "sus2" => "åben og luftig",
        "m" => "samme grundtone i mol",
        _ => "samme grundtone, enkel",
    };
}
