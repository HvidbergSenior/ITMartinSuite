using System.Text.RegularExpressions;

namespace ITMartinBudget.Application.Helpers;

public sealed record CategoryDuplicateGroup(List<string> Names, string SuggestedTargetName);

// Found live 2026-09-07: asking AI to spot every near-duplicate category
// name in one pass over ~240 unsorted names misses obvious cases (e.g. the
// same person "Fie Tandrup" spread across "Debetkort Mob.Pay*Fie Tandru",
// "MobilePay MobilePay Fie Tandru", "MOBILEPAY FIE TANDRU", "MobilePay Fie
// Tandrup Rokkjær" - a bank-export prefix/truncation problem, not something
// that needs real-world judgment). This is the mechanical, zero-AI-cost
// half of merge-suggestion: strip known transaction-channel noise and
// trailing order/reference numbers, then group names that become identical
// or where one is a prefix of the other (handles the bank's own field-length
// truncation). Reserve the AI call (SuggestMergesAsync) for merges that
// genuinely need judgment - different gas station brands into "Benzin",
// where there's no shared substring to strip down to.
public static class CategoryDuplicateFinder
{
    private static readonly string[] NoisePhrases =
    [
        "mobilepay", "debetkort", "mob.pay*", "mob pay", "forretning:", "debetko",
    ];

    public static string Normalize(string name)
    {
        var s = name.ToLowerInvariant();
        foreach (var noise in NoisePhrases)
        {
            s = s.Replace(noise, " ");
        }
        // "BS " (Betalingsservice, a direct-debit prefix) only means
        // anything as a leading marker - stripping it anywhere in the
        // string would wrongly eat the "bs" in unrelated words.
        s = Regex.Replace(s, @"^bs\s+", " ");
        // Trailing order/reference numbers (4+ digits) - "imusic.dk 11338750"
        // and "imusic.dk 10915939" should normalize to the same key.
        s = Regex.Replace(s, @"\d{4,}", " ");
        s = Regex.Replace(s, @"[^a-zæøåa-z0-9\s]", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    public static List<CategoryDuplicateGroup> FindGroups(
        IReadOnlyList<string> categoryNames)
    {
        // Below this, a coincidental short-prefix match (e.g. "kr" matching
        // lots of unrelated things) becomes a real risk - 4 is short enough
        // to still catch real short Danish brand names ("rema", "netto")
        // while still excluding 1-3 character noise.
        const int minPrefixLength = 4;

        var withKeys = categoryNames
            .Select(name => (Name: name, Key: Normalize(name)))
            .Where(x => x.Key.Length >= minPrefixLength)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var groups = new List<List<string>>();
        var currentGroup = new List<string>();
        string? currentKey = null;

        foreach (var (name, key) in withKeys)
        {
            if (currentKey is null || key == currentKey || key.StartsWith(currentKey, StringComparison.Ordinal) || currentKey.StartsWith(key, StringComparison.Ordinal))
            {
                currentGroup.Add(name);
                // Keep the shorter key as the group's key - a longer key
                // that's a strict extension of the current one (truncation)
                // should still match the *next* item against the short form.
                if (currentKey is null || key.Length < currentKey.Length)
                {
                    currentKey = key;
                }
            }
            else
            {
                if (currentGroup.Count >= 2) groups.Add(currentGroup);
                currentGroup = [name];
                currentKey = key;
            }
        }
        if (currentGroup.Count >= 2) groups.Add(currentGroup);

        return groups
            .Select(g => new CategoryDuplicateGroup(g, PickTargetName(g)))
            .ToList();
    }

    // Shortest name in the group reads as the "cleanest" one most of the
    // time (least bank-export noise still attached) - not perfect, but a
    // reasonable default the user can always retype on the Flet page.
    private static string PickTargetName(List<string> group) =>
        group.OrderBy(n => n.Length).First();
}
