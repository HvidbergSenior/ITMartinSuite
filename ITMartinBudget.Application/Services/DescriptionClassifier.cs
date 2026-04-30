namespace ITMartinBudget.Application.Services;

public static class DescriptionClassifier
{
    private static readonly string[] CompanyKeywords =
    {
        "aps","a/s","as","lidl","rema","netto","føtex","bilka",
        "matas","spotify","apple","google","bet365","betfair",
        "tank","ok","shell","circle k"
    };

    public static bool IsLikelyPerson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 2 || words.Length > 3)
            return false;

        if (text.Any(char.IsDigit))
            return false;

        var lower = text.ToLowerInvariant();

        // ❌ block known junk words
        var invalid = new[] { "dk", "aps", "as", "ltd" };
        if (words.Any(w => invalid.Contains(w)))
            return false;

        // ❌ block company keywords
        if (CompanyKeywords.Any(k => lower.Contains(k)))
            return false;

        // ✅ must look like real names (letters only, min length)
        return words.All(w => w.Length > 2 && w.All(char.IsLetter));
    }
}