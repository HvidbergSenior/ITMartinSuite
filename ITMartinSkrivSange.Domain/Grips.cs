namespace ITMartinSkrivSange.Domain;

/// <param name="Frets">Per string, low E to high e: -1 = not played, 0 = open, 1+ = fret (counted from the capo).</param>
/// <param name="Fingers">Per string: 0 = no finger, 1 = index ... 4 = little finger.</param>
public sealed record Grip(string Chord, IReadOnlyList<int> Frets, IReadOnlyList<int> Fingers)
{
    /// <summary>"x32010" - the usual short way to write a grip.</summary>
    public string Short => string.Concat(Frets.Select(f => f < 0 ? "x" : f.ToString()));
}

// How each open grip is played (user 2026-10-14: "the chords should be shown as the guitar Greb").
// The page draws the chord box from this, and the strum player plays these exact strings.
public static class Grips
{
    private static readonly Dictionary<string, Grip> All = new[]
    {
        G("C",     [-1, 3, 2, 0, 1, 0], [0, 3, 2, 0, 1, 0]),
        G("D",     [-1, -1, 0, 2, 3, 2], [0, 0, 0, 1, 3, 2]),
        G("E",     [0, 2, 2, 1, 0, 0],  [0, 2, 3, 1, 0, 0]),
        G("G",     [3, 2, 0, 0, 0, 3],  [2, 1, 0, 0, 0, 3]),
        G("A",     [-1, 0, 2, 2, 2, 0], [0, 0, 1, 2, 3, 0]),
        G("Am",    [-1, 0, 2, 2, 1, 0], [0, 0, 2, 3, 1, 0]),
        G("Dm",    [-1, -1, 0, 2, 3, 1], [0, 0, 0, 2, 3, 1]),
        G("Em",    [0, 2, 2, 0, 0, 0],  [0, 2, 3, 0, 0, 0]),
        G("Cadd9", [-1, 3, 2, 0, 3, 0], [0, 2, 1, 0, 3, 0]),
        G("Dsus4", [-1, -1, 0, 2, 3, 3], [0, 0, 0, 1, 2, 3]),
        G("Asus2", [-1, 0, 2, 2, 0, 0], [0, 0, 1, 2, 0, 0]),
        G("Em7",   [0, 2, 2, 0, 3, 0],  [0, 1, 2, 0, 3, 0]),
        G("Am7",   [-1, 0, 2, 0, 1, 0], [0, 0, 2, 0, 1, 0]),
        G("Fmaj7", [-1, -1, 3, 2, 1, 0], [0, 0, 3, 2, 1, 0]),
        G("A7",    [-1, 0, 2, 0, 2, 0], [0, 0, 2, 0, 3, 0]),
        G("D7",    [-1, -1, 0, 2, 1, 2], [0, 0, 0, 2, 1, 3]),
        G("E7",    [0, 2, 0, 1, 0, 0],  [0, 2, 0, 1, 0, 0]),
        G("B7",    [-1, 2, 1, 2, 0, 2], [0, 2, 1, 3, 0, 4]),
        G("G7",    [3, 2, 0, 0, 0, 1],  [3, 2, 0, 0, 0, 1]),
    }.ToDictionary(g => g.Chord);

    public static Grip? For(string chord) => All.GetValueOrDefault(chord);

    private static Grip G(string chord, int[] frets, int[] fingers) => new(chord, frets, fingers);
}
