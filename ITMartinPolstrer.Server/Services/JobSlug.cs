namespace ITMartinPolstrer.Server.Services;

// Extracted from Index.razor's create-job handler so the slugging rules are
// directly testable - the NAS folder name a job's photos live under depends
// on getting this right (filesystem-safe, stable, and not collision-prone
// when two jobs share a title like "Lænestol").
public static class JobSlug
{
    public static string From(string title)
    {
        var cleaned = new string(title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        cleaned = cleaned.Trim('-');
        return $"{cleaned}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
