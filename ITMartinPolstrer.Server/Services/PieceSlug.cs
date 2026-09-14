namespace ITMartinPolstrer.Server.Services;

// Filesystem-safe, stable folder name for a piece's media. Two pieces with
// the same title ("Lænestol") must not collide, hence the random suffix.
public static class PieceSlug
{
    public static string From(string title)
    {
        var cleaned = new string(title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        cleaned = cleaned.Trim('-');
        if (cleaned.Length == 0) cleaned = "moebel";
        return $"{cleaned}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
