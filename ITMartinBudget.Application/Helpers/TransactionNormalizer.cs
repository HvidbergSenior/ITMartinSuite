using System.Text.RegularExpressions;

namespace ITMartinBudget.Application.Helpers;

public static class TransactionNormalizer
{
    public static string Normalize(
        string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        input =
            input.ToLowerInvariant();

        input = input

            .Replace("æ", "ae")
            .Replace("ø", "oe")
            .Replace("å", "aa");

        // Remove punctuation
        input = Regex.Replace(
            input,
            @"[^\w\s]",
            " ");

        // Collapse spaces
        input = Regex.Replace(
            input,
            @"\s+",
            " ");

        return input.Trim();
    }
}