namespace ITMartinBudget.Application.Helpers;

// Requested explicitly: "All gasstations in one cat. All dagligvarebutikker
// in one cat." Different from CategoryDuplicateFinder - that one catches the
// *same* merchant spread across formatting noise (prefix-based), this one
// catches *different* brands of the same broad kind of spending (Shell, Q8,
// Ingo are unrelated strings - there's no shared substring to strip down to,
// only real-world knowledge that they're all gas stations). A curated
// keyword list is more reliable here than asking AI to reconstruct that
// knowledge while also scanning ~150+ unsorted names in one pass.
public static class KnownBrandCategoryGrouper
{
    private static readonly (string TargetName, string[] Keywords)[] Groups =
    [
        ("Benzin", ["shell", "q8", "uno-x", "unox", "ingo", "f24", "goeasy", "go easy", "circle k", "circlek", "ok benzin", "ok plus"]),
        ("Dagligvarer", ["rema", "netto", "føtex", "foetex", "lidl", "kvickly", "meny", "fakta", "spar", "superbrugsen", "brugsen", "aldi", "irma", "bilka", "coop365", "coop 365", "7-eleven"]),
        // "leas" - a genuine shared substring (proleasing.nu, leasingaftale,
        // leasing bil), not a prefix, so CategoryDuplicateFinder misses it.
        ("Leasing", ["leas"]),
        ("Abonnementer", ["telenor", "telia", "tdc", "hiper", "3 mobil", "cbb mobil", "oister", "callme", "yousee"]),
        ("Bøder", ["bøde", "p-bøde", "parkeringsbøde", "fartbøde", "afgift for manglende"]),
        ("Parkering", ["parkering", "p-afgift", "apcoa", "easypark", "parkster"]),
    ];

    public static List<CategoryDuplicateGroup> FindGroups(
        IReadOnlyList<string> categoryNames)
    {
        var result = new List<CategoryDuplicateGroup>();

        foreach (var (targetName, keywords) in Groups)
        {
            var matches = categoryNames
                .Where(name => keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Skip if everything already IS the target name (a single
            // "Benzin" category with nothing else to fold in is not a group).
            if (matches.Count(m => !string.Equals(m, targetName, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                continue;
            }

            if (matches.Count >= 2)
            {
                result.Add(new CategoryDuplicateGroup(matches, targetName));
            }
        }

        return result;
    }
}
