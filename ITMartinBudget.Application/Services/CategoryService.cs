using System.Text.RegularExpressions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class CategoryService : ICategoryService
{
    public Task<Category> DetectAsync(string text)
    {
        text = Normalize(text);

        // =====================================
        // PURE NUMBER REFERENCES
        // Example:
        // 9490 71557243
        // 7633 8119308
        // =====================================

        if (LooksLikeTransferReference(text))
        {
            return Task.FromResult(Category.Transfer);
        }

        foreach (var rule in CategoryRules.Rules
                     .OrderByDescending(x => x.Key.Length))
        {
            var pattern = Normalize(rule.Key);

            // Exact word matching
            if (CategoryRules.ExactWordRules.Contains(pattern))
            {
                if (ContainsExactWord(text, pattern))
                {
                    return Task.FromResult(rule.Value);
                }

                continue;
            }

            // Normal contains matching
            if (text.Contains(pattern))
            {
                return Task.FromResult(rule.Value);
            }
        }

        return Task.FromResult(Category.Other);
    }

    private static bool LooksLikeTransferReference(
        string text)
    {
        // remove spaces
        var cleaned =
            text.Replace(" ", "");

        // only digits
        return Regex.IsMatch(
            cleaned,
            @"^\d{8,}$");
    }

    private static bool ContainsExactWord(
        string input,
        string word)
    {
        return Regex.IsMatch(
            input,
            $@"(?<![a-zæøå]){Regex.Escape(word)}(?![a-zæøå])",
            RegexOptions.IgnoreCase);
    }

    private static string Normalize(string text)
    {
        return text
            .ToLowerInvariant()
            .Trim()
            .Replace("æ", "ae")
            .Replace("ø", "oe")
            .Replace("å", "aa");
    }
}