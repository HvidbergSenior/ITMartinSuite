namespace ITMartinMusicHelper.Web.Data;

public static class ChordNames
{
    // Danish note names (German system: H = B natural, B = Bb)
    private static readonly Dictionary<string, string> NoteMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = "C", ["C#"] = "Cis", ["Db"] = "Des",
        ["D"] = "D", ["D#"] = "Dis", ["Eb"] = "Es",
        ["E"] = "E", ["F"] = "F",
        ["F#"] = "Fis", ["Gb"] = "Ges",
        ["G"] = "G", ["G#"] = "Gis", ["Ab"] = "As",
        ["A"] = "A", ["A#"] = "Ais", ["Bb"] = "B",
        ["B"] = "H",
    };

    // Returns e.g. "G-dur", "A-mol", "Fis-dur", "H-mol7"
    public static string FullName(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return symbol;

        // Extract root (1 or 2 chars)
        int i = 1;
        if (symbol.Length > 1 && (symbol[1] == '#' || symbol[1] == 'b'))
            i = 2;
        var root = symbol[..i];
        var rest = symbol[i..];

        if (!NoteMap.TryGetValue(root, out var danishRoot))
            danishRoot = root;

        // Quality
        string quality;
        string suffix = "";

        if (rest.StartsWith("maj7"))        { quality = "-dur"; suffix = "maj7"; rest = rest[4..]; }
        else if (rest.StartsWith("maj"))    { quality = "-dur"; suffix = "maj";  rest = rest[3..]; }
        else if (rest.StartsWith("m7"))     { quality = "-mol"; suffix = "7";    rest = rest[2..]; }
        else if (rest.StartsWith("m"))      { quality = "-mol"; suffix = "";     rest = rest[1..]; }
        else if (rest.StartsWith("dim"))    { quality = "-dim"; suffix = "";     rest = rest[3..]; }
        else if (rest.StartsWith("aug"))    { quality = "-aug"; suffix = "";     rest = rest[3..]; }
        else if (rest.StartsWith("sus"))    { quality = "";     suffix = rest;   rest = ""; }
        else if (rest.StartsWith("add"))    { quality = "-dur"; suffix = rest;   rest = ""; }
        else                                { quality = "-dur"; suffix = rest;   rest = ""; }

        return $"{danishRoot}{quality}{suffix}{rest}";
    }

    // Short display: just the root in Danish notation, keep quality suffix
    public static string DanishSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return symbol;
        int i = 1;
        if (symbol.Length > 1 && (symbol[1] == '#' || symbol[1] == 'b'))
            i = 2;
        var root = symbol[..i];
        var rest = symbol[i..];
        if (!NoteMap.TryGetValue(root, out var danishRoot)) danishRoot = root;
        return danishRoot + rest;
    }
}
