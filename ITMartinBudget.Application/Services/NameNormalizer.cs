namespace ITMartinBudget.Application.Services;

public static class NameNormalizer
{
    public static string NormalizePersonName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var parts = name
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 1)
            .ToList();

        if (parts.Count == 0)
            return string.Empty;

        if (parts.Count == 1)
            return parts[0];

        // 🔥 ALWAYS first + last
        return $"{parts.First()} {parts.Last()}";
    }
}