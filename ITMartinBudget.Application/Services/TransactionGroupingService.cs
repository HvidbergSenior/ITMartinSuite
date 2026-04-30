using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public static class TransactionGroupingService
{
    public static string GetGroupingKey(BankTransaction t)
    {
        Console.WriteLine($"[GROUP] RAW: {t.Description}");

        var source = DescriptionClassifier.IsLikelyPerson(t.MobilePayName)
            ? t.MobilePayName
            : t.Description;
        
        if (string.IsNullOrWhiteSpace(source))
            source = t.Description;

        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        source = source.ToLowerInvariant();

        var noise = new[] { "mobilepay", "vd", "aps" };
        foreach (var n in noise)
            source = source.Replace(n, "");

        source = new string(source
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray());

        source = source.Trim();

        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var words = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= 2 && DescriptionClassifier.IsLikelyPerson(source))
        {
            var normalized = NameNormalizer.NormalizePersonName(source);
            Console.WriteLine($"[GROUP-PERSON] {normalized}");
            return normalized;
        }

        var fallback = NormalizeText(source);

        Console.WriteLine($"[GROUP-TEXT] {fallback}");
        return fallback;
    }

    private static string NormalizeText(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2);

        return string.Join(" ", words).Trim();
    }
}