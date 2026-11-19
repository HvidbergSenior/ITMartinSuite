using System.Text.RegularExpressions;

namespace ITMartinHjem.Domain;

// "Det leder folk efter" on bogshoppen.dk (user 2026-11-19: "lots of customers are looking for products ... show what
// people are looking for"). Input = the interest texts from Kunder for those who said yes to Bogshoppen, no names.
// A text looks like "Bøger, Musik på cd | Isaac Asimov" (ticked groups | own words), several apps joined by " ; ".
public static class BogshoppenWishes
{
    public sealed record Group(string Title, int Count);
    public sealed record Summary(int People, IReadOnlyList<Group> Groups, IReadOnlyList<string> Wishes);

    public const int MaxWish = 140;

    // Titles from the first sign-up form (October) count under today's groups.
    private static readonly Dictionary<string, string> OldTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Anders And-blade"] = "Anders And",
        ["Tegneserier og album"] = "Tegneserier",
        ["Blade og magasiner"] = "Blade og magasiner",
        ["Tegnefilm"] = "Film på dvd og Blu-ray",
        ["Konsolspil"] = "Konsolspil",
    };

    private static readonly Regex Email = new(@"\S+@\S+", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"(\+?\d[\d \-]{6,}\d)", RegexOptions.Compiled);
    private static readonly Regex Url = new(@"https?://\S+|www\.\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static Summary Summarize(IEnumerable<string> interests)
    {
        var titles = BogshoppenRules.Products.Select(p => p.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wishes = new List<string>();
        var people = 0;
        foreach (var text in interests)
        {
            var any = false;
            foreach (var part in (text ?? "").Split(" ; ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsItMartin(part)) continue;
                var bar = part.IndexOf(" | ", StringComparison.Ordinal);
                var head = bar < 0 ? part : part[..bar];
                var note = bar < 0 ? "" : part[(bar + 3)..];

                // "Superhelte fra DC og Marvel, på engelsk" has a comma inside the title - join it back.
                var picked = Picked(head, titles);
                if (picked.Count == 0 && bar < 0) note = part;   // no box ticked: the whole text is their own words
                foreach (var t in picked) { counts[t] = counts.GetValueOrDefault(t) + 1; any = true; }
                var w = Clean(note);
                if (w.Length > 0) { wishes.Add(w); any = true; }
            }
            if (any) people++;
        }
        var groups = counts.Select(kv => new Group(kv.Key, kv.Value))
            .OrderByDescending(g => g.Count).ThenBy(g => g.Title, StringComparer.Create(new System.Globalization.CultureInfo("da-DK"), true)).ToList();
        return new Summary(people, groups, wishes.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static List<string> Picked(string head, HashSet<string> titles)
    {
        var picked = new List<string>();
        var rest = head;
        // A title with a comma in it is taken out whole first.
        foreach (var t in titles.Where(t => t.Contains(',')))
            if (rest.Contains(t, StringComparison.OrdinalIgnoreCase)) { picked.Add(t); rest = rest.Replace(t, "", StringComparison.OrdinalIgnoreCase); }
        // Only a list of known groups counts as ticked boxes; anything else means it is their own words.
        foreach (var i in rest.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (i.StartsWith("Andet", StringComparison.OrdinalIgnoreCase)) continue;
            var t = titles.FirstOrDefault(x => string.Equals(x, i, StringComparison.OrdinalIgnoreCase))
                ?? (OldTitles.TryGetValue(i, out var mapped) ? mapped : null);
            if (t is null) return [];
            if (!picked.Contains(t, StringComparer.OrdinalIgnoreCase)) picked.Add(t);
        }
        return picked;
    }

    // An itmartin.dk sign-up from the same person: an app name or the help topics - not something to buy.
    private static bool IsItMartin(string part) =>
        TrialRules.Topics.Any(t => part.Contains(t, StringComparison.OrdinalIgnoreCase))
        || part.Contains("Bogshoppens nye måder", StringComparison.OrdinalIgnoreCase)
        || Regex.IsMatch(part, @"\bapps?\b", RegexOptions.IgnoreCase)   // "Budget app, bedre end Spiiri" is about ITMartin, not a wish
        || Regex.IsMatch(part, @"^(itmartin\.dk|[A-ZÆØÅ][a-zæøå]+[A-ZÆØÅ][a-zæøå]+|Stem|Tilbud|Budget|Snak|Forløbet|Polstrer)$");

    /// <summary>Their own words, safe to show: no e-mails, phone numbers or links, one line, at most 140 characters.</summary>
    public static string Clean(string? note)
    {
        var t = Url.Replace(Phone.Replace(Email.Replace(note ?? "", ""), ""), "");
        t = Spaces.Replace(t, " ").Trim(' ', ',', '.', '-', '|');
        if (t.Length < 3) return "";
        return t.Length <= MaxWish ? t : t[..t.LastIndexOf(' ', MaxWish - 1)].TrimEnd(',', '.') + " …";
    }
}
