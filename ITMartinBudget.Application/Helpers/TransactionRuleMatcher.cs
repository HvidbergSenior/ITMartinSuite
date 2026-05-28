using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Helpers;

public static class TransactionRuleMatcher
{
    public static TransactionRule? FindBestMatch(
        string normalizedText,
        IEnumerable<TransactionRule> rules)
    {
        // =====================================
        // EXACT
        // =====================================

        var exactMatch =
            rules.FirstOrDefault(x =>
                x.ComparingType == ComparingType.Exact &&
                normalizedText.Equals(
                    x.Pattern,
                    StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            return exactMatch;
        }

        // =====================================
        // WORD
        // =====================================

        var wordMatch =
            rules.FirstOrDefault(x =>
                x.ComparingType == ComparingType.Word &&
                ContainsWholePhrase(
                    normalizedText,
                    x.Pattern));

        if (wordMatch is not null)
        {
            return wordMatch;
        }

        // =====================================
        // CONTAINS
        // =====================================

        var containsMatch =
            rules.FirstOrDefault(x =>
                x.ComparingType == ComparingType.Contains &&
                normalizedText.Contains(
                    x.Pattern,
                    StringComparison.OrdinalIgnoreCase));

        return containsMatch;
    }

    private static bool ContainsWholePhrase(
        string text,
        string phrase)
    {
        var paddedText =
            $" {text} ";

        var paddedPhrase =
            $" {phrase} ";

        return paddedText.Contains(
            paddedPhrase,
            StringComparison.OrdinalIgnoreCase);
    }
}