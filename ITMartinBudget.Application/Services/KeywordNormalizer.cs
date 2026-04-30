namespace ITMartinBudget.Application.Services;

public static class KeywordNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text
            .ToLowerInvariant()
            .Trim();
    }
}