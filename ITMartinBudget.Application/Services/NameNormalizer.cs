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

        // 🔥 ALWAYS prioritize known stable patterns

        // 3+ names → use FIRST + SECOND (not last!)
        if (parts.Count >= 3)
            return $"{parts[0]} {parts[1]}";

        // 2 names → use both
        if (parts.Count == 2)
            return $"{parts[0]} {parts[1]}";

        return parts[0];
    }
}