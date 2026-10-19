namespace ITMartinSkrivSange.Domain;

// Martin's own chord progressions (user 2026-10-14: "I make for instance Am, G, Am, G ... I should be able to pick that
// and it should be saved as MartinChord something"). Saved once, then offered in every song next to the library.
public sealed record OwnProgression(Guid Id, string Name, IReadOnlyList<string> Chords, DateTime CreatedAt)
{
    public const string NamePrefix = "MartinChord", IdPrefix = "own-";
    public const int MaxName = 40;

    /// <summary>What a song part stores in ProgressionId when it uses this one.</summary>
    public string ProgressionId => IdPrefix + Id.ToString("N");

    public static bool IsOwnId(string? progressionId) => progressionId?.StartsWith(IdPrefix) == true;

    /// <summary>
    /// The saved one with the same chords, or a new one named <paramref name="name"/> (or "MartinChord n" when empty).
    /// </summary>
    public static (OwnProgression Progression, bool IsNew) FindOrCreate(IReadOnlyList<OwnProgression> existing, IReadOnlyList<string> chords, string? name, DateTime nowUtc)
    {
        if (chords.Count is 0 or > Song.MaxChords) throw new SongException($"Skriv 1-{Song.MaxChords} akkorder.");
        if (existing.FirstOrDefault(p => p.Chords.SequenceEqual(chords)) is { } same) return (same, false);

        var n = (name ?? "").Trim();
        if (n.Length == 0) n = NextName(existing);
        if (n.Length > MaxName) n = n[..MaxName];
        return (new OwnProgression(Guid.NewGuid(), n, chords.ToList(), nowUtc), true);
    }

    /// <summary>"MartinChord 1", then 2, 3 ... - one higher than the highest number used so far.</summary>
    public static string NextName(IReadOnlyList<OwnProgression> existing)
    {
        var used = existing
            .Select(p => p.Name.StartsWith(NamePrefix + " ") && int.TryParse(p.Name[(NamePrefix.Length + 1)..], out var i) ? i : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{NamePrefix} {used + 1}";
    }
}

// Reading chords the way they are typed: "Am, G, Am, G", "am g am g", "Am - G | Am G".
public static class ChordInput
{
    private static readonly char[] Separators = [' ', ',', ';', '-', '|', '/', '\t', '\n', '\r'];

    /// <summary>The chords when the whole text is chords (e.g. lyric notes holding "Am, G, Am, G, E"), else null.</summary>
    public static IReadOnlyList<string>? TryParse(string? text)
    {
        try { return Parse(text); }
        catch (SongException) { return null; }
    }

    /// <exception cref="SongException">When a chord is not one of the open grips (the capo does the key).</exception>
    public static IReadOnlyList<string> Parse(string? text)
    {
        var words = (text ?? "").Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) throw new SongException("Skriv akkorderne, fx Am G Am G.");
        if (words.Length > Song.MaxChords) throw new SongException($"Højst {Song.MaxChords} akkorder i en del.");

        var chords = new List<string>();
        foreach (var w in words)
        {
            // Case doesn't matter ("am" = "Am", "bm" = "Bm") - no two grips differ only by case. Barre grips too since 2026-10-19.
            var grip = Chords.OpenGrips.FirstOrDefault(g => string.Equals(g, w, StringComparison.OrdinalIgnoreCase))
                       ?? Grips.Names.FirstOrDefault(g => string.Equals(g, w, StringComparison.OrdinalIgnoreCase))
                       ?? (Grips.For(char.ToUpperInvariant(w[0]) + w[1..]) is { } found ? found.Chord : null);
            if (grip is null)
                throw new SongException($"{w} kendes ikke som greb. Brug fx {string.Join(", ", Chords.OpenGrips.Take(8))} - og capo for at skifte toneart.");
            chords.Add(grip);
        }
        return chords;
    }
}
