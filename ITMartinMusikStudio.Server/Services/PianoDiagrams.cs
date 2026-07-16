namespace ITMartinMusikStudio.Server.Services;

// Unlike guitar (many equally valid finger shapes per chord), a piano chord
// is just its notes - root + quality intervals - mapped onto absolute keys,
// so this needs no curated shape table, only music theory. One octave is
// enough to show which keys to press; real playing spreads octaves, but the
// point here is "which notes", not a literal fingering.
public static class PianoDiagrams
{
    private static readonly string[] Notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly Dictionary<string, int> NoteToSemitone;

    static PianoDiagrams()
    {
        NoteToSemitone = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Notes.Length; i++)
            NoteToSemitone[Notes[i]] = i;
        NoteToSemitone["Db"] = 1;
        NoteToSemitone["Eb"] = 3;
        NoteToSemitone["Gb"] = 6;
        NoteToSemitone["Ab"] = 8;
        NoteToSemitone["Bb"] = 10;
    }

    // Semitone intervals from the root, by chord quality suffix.
    private static readonly (string Suffix, int[] Intervals)[] Qualities =
    [
        ("maj7", [0, 4, 7, 11]),
        ("m7",   [0, 3, 7, 10]),
        ("7",    [0, 4, 7, 10]),
        ("sus2", [0, 2, 7]),
        ("sus4", [0, 5, 7]),
        ("add9", [0, 4, 7, 2]),
        ("dim",  [0, 3, 6]),
        ("aug",  [0, 4, 8]),
        ("m",    [0, 3, 7]),
        ("",     [0, 4, 7]), // major - fallback, must be last
    ];

    public static string? GetSvg(string? chordName)
    {
        if (string.IsNullOrEmpty(chordName)) return null;

        string root, quality;
        if (chordName.Length >= 2 && chordName[1] is '#' or 'b')
        { root = chordName[..2]; quality = chordName[2..]; }
        else
        { root = chordName[..1]; quality = chordName[1..]; }

        if (!NoteToSemitone.TryGetValue(root, out int rootSt)) return null;

        var intervals = Qualities.FirstOrDefault(q => quality.Equals(q.Suffix, StringComparison.OrdinalIgnoreCase)).Intervals
            ?? Qualities.Last().Intervals; // unrecognized quality - default to major

        var pressed = intervals.Select(i => (rootSt + i) % 12).ToHashSet();

        return RenderSvg(pressed);
    }

    private static string RenderSvg(HashSet<int> pressedSemitones)
    {
        // One octave, C to B. White keys: C D E F G A B (semitones 0,2,4,5,7,9,11).
        int[] whiteSemitones = [0, 2, 4, 5, 7, 9, 11];
        // Black key semitone + which white-key index it sits after (0-based).
        (int Semitone, int AfterWhiteIndex)[] blackKeys = [(1, 0), (3, 1), (6, 3), (8, 4), (10, 5)];

        const int whiteW = 20, whiteH = 70, blackW = 13, blackH = 44;
        int totalW = whiteW * whiteSemitones.Length;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {totalW} {whiteH}' width='{totalW}' height='{whiteH}'>");

        for (int i = 0; i < whiteSemitones.Length; i++)
        {
            bool on = pressedSemitones.Contains(whiteSemitones[i]);
            var fill = on ? "#3b82f6" : "#ffffff";
            sb.Append($"<rect x='{i * whiteW}' y='0' width='{whiteW}' height='{whiteH}' fill='{fill}' stroke='#333' stroke-width='1'/>");
        }

        foreach (var (semitone, afterWhiteIndex) in blackKeys)
        {
            bool on = pressedSemitones.Contains(semitone);
            var fill = on ? "#1d4ed8" : "#111827";
            int x = (afterWhiteIndex + 1) * whiteW - blackW / 2;
            sb.Append($"<rect x='{x}' y='0' width='{blackW}' height='{blackH}' fill='{fill}' stroke='#000' stroke-width='1'/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
