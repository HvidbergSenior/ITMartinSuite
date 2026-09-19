namespace ITMartinPolstrer.Core.Services;

// Filesystem-safe, stable folder name for a piece's media. Two pieces with
// the same title ("Lænestol") must not collide, hence the random suffix.
public static class PieceSlug
{
    public static string From(string title)
    {
        // ASCII only: the slug travels in a redirect header, where æøå blow up.
        var lower = title.Trim().ToLowerInvariant().Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa").Replace("é", "e").Replace("ü", "u").Replace("ö", "oe").Replace("ä", "ae");
        var cleaned = new string(lower
            .Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-')
            .ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        cleaned = cleaned.Trim('-');
        if (cleaned.Length == 0) cleaned = "moebel";
        return $"{cleaned}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
