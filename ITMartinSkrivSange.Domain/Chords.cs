namespace ITMartinSkrivSange.Domain;

// Chord names and the guitar rule Martin plays by (user 2026-09: always the same open grips + capo, never barre keys).
public static class Chords
{
    private static readonly string[] Sharps = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    // How a key is usually written on a chord sheet.
    private static readonly string[] Display = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    /// <summary>The open grips a song is written in. No F, no Bm, no barre.</summary>
    public static readonly IReadOnlyList<string> OpenGrips =
        ["C", "D", "E", "G", "A", "Am", "Dm", "Em", "Cadd9", "Dsus4", "Asus2", "Em7", "Am7", "Fmaj7", "A7", "D7", "E7", "B7", "G7"];

    public static bool IsOpenGrip(string chord) => OpenGrips.Contains(chord);

    /// <returns>The root (C, F#, Bb ...) and the rest (m, 7, sus4 ...), or null when it isn't a chord name.</returns>
    public static (string Root, string Suffix)? Split(string chord)
    {
        if (string.IsNullOrWhiteSpace(chord) || chord[0] is < 'A' or > 'G') return null;
        var root = chord.Length > 1 && chord[1] is '#' or 'b' ? chord[..2] : chord[..1];
        return (root, chord[root.Length..]);
    }

    public static int? RootIndex(string chord)
    {
        if (Split(chord) is not { } s) return null;
        var normal = s.Root switch { "Db" => "C#", "Eb" => "D#", "Gb" => "F#", "Ab" => "G#", "Bb" => "A#", var r => r };
        var i = Array.IndexOf(Sharps, normal);
        return i < 0 ? null : i;
    }

    public static bool IsMinor(string chord) =>
        Split(chord) is { } s && s.Suffix.StartsWith('m') && !s.Suffix.StartsWith("maj");

    public static string Transpose(string chord, int semitones)
    {
        if (semitones == 0 || Split(chord) is not { } s || RootIndex(chord) is not { } i) return chord;
        return Display[((i + semitones) % 12 + 12) % 12] + s.Suffix;
    }

    public static IReadOnlyList<string> Transpose(IEnumerable<string> chords, int semitones) =>
        chords.Select(c => Transpose(c, semitones)).ToList();

    /// <summary>The major key a progression belongs to: Em -> G, Am -> C, Dm -> F (a minor key shares its notes with the major key 3 frets up).</summary>
    public static string MajorFamily(string home) =>
        IsMinor(home) && RootIndex(home) is { } i ? Display[(i + 3) % 12] : Split(home)?.Root ?? home;

    /// <summary>The key as it is said: G, Em ... (A7 -> A).</summary>
    public static string KeyName(string home) =>
        Split(home) is { } s ? s.Root + (IsMinor(home) ? "m" : "") : home;
}

// A capo raises every open grip by one semitone per fret (user rule: change the key with the capo, keep the grips).
public static class Capo
{
    public const int Max = 7;

    public static string Sounding(string grip, int capo) => Chords.Transpose(grip, capo);

    /// <summary>"A" for a song gripped in G with the capo on 2.</summary>
    public static string SoundingKey(string homeGrip, int capo) => Chords.KeyName(Chords.Transpose(homeGrip, capo));
}
