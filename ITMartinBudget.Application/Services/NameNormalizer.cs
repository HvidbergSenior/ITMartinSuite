using ITMartinBudget.Application.Interfaces;

namespace ITMartinBudget.Application.Services;

public class NameNormalizer : INameNormalizer
{
    public string Normalize(string? name)
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

        // 🔥 3+ names → first + second (stable identity)
        if (parts.Count >= 3)
            return $"{parts[0]} {parts[1]}";

        // 🔥 2 names → both
        if (parts.Count == 2)
            return $"{parts[0]} {parts[1]}";

        // 🔥 fallback
        return parts[0];
    }
}