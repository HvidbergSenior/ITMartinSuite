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

        // ❌ known business keywords
        if (CompanyKeywords.Any(k => lower.Contains(k)))
            return false;

        // ❌ blacklist obvious non-names
        var blacklist = new[]
        {
            "spa", "cafe", "kfc", "pizza", "bank", "invest",
            "investering", "shop", "store", "market"
        };

        if (words.Any(w => blacklist.Contains(w)))
            return false;

        // ❌ words must look like real names (not too long)
        if (words.Any(w => w.Length > 12))
            return false;

        // ✅ first + last name structure
        return words.All(w =>
            w.Length > 2 &&
            char.IsLetter(w[0]) &&
            w.All(char.IsLetter));
    }
}