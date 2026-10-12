namespace ITMartinVareinfo.Domain;

// EAN / ISBN rules.
public static class Barcodes
{
    public static string Clean(string code) => new(code.Where(char.IsDigit).ToArray());

    // Books before 2007 carry a 10-digit ISBN (last digit may be X); Google Books and the sites want the 13-digit one.
    public static string ToIsbn13(string typed)
    {
        var raw = new string(typed.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()).ToUpperInvariant();
        if (raw.Length != 10) return Clean(raw);
        var core = "978" + raw[..9];
        var sum = core.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return core + (10 - sum % 10) % 10;
    }

    public static bool IsIsbn(string code) => code.Length == 13 && (code.StartsWith("978") || code.StartsWith("979"));
}
