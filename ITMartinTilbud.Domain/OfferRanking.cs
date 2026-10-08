namespace ITMartinTilbud.Domain;

/// <summary>Which offers answer a search, and in what order. Like with like (kr/kg before kr/stk), cheapest first,
/// the searched thing before flavoured kinds of it, the described kind ("økologisk") first.</summary>
public static class OfferRanking
{
    /// <summary>Tjek's search handles several words badly ("økologisk mælk" gave mostly kefir), so a search with describing
    /// words also asks for the thing itself - the last word ("mælk"). Relevant() then puts the right kind first.</summary>
    public static IReadOnlyList<string> SearchTerms(string query)
    {
        query = query.Trim();
        var head = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        return head.Equals(query, StringComparison.OrdinalIgnoreCase) ? [query] : [query, head];
    }

    // Only like with like: the most common unit first (kr/kg for coffee, kr/l for milk), cheapest first within it;
    // then the other units, and offers without a size last. Capsules at 3 kr/stk must not beat coffee at 60 kr/kg.
    public static List<Offer> Sorted(IEnumerable<Offer> offers)
    {
        var list = offers.ToList();
        var rank = list.Where(x => x.UnitLabel is not null).GroupBy(x => x.UnitLabel!)
            .OrderByDescending(g => g.Count()).Select((g, i) => (g.Key, i)).ToDictionary(t => t.Key, t => t.i);
        return list.OrderBy(x => x.UnitLabel is { } u ? rank[u] : int.MaxValue).ThenBy(x => x.UnitPrice ?? x.Price).ToList();
    }

    // Tjek's search is loose ("smør" also brought potatoes): keep offers whose title or text has the searched thing;
    // with 3+ title matches only those (margarine that says "smør" in its text is not butter). Order (per kg) is kept.
    // If nothing has the words, show what Tjek found rather than nothing.
    // Non-food goes (user 2026-10-06: "mælk" gave shower gel), unless that is what was searched for, and flavoured kinds
    // (kakaomælk, jordbæryoghurt) are marked Variant and go last - the page folds them away.
    // Several words: the LAST word is the thing ("mælk") and decides what is found; the words before it describe the kind
    // ("økologisk") and only sort: the offers that have them come first (Matches).
    public static List<Offer> Relevant(List<Offer> list, string query)
    {
        var q = query.ToLowerInvariant();
        var all = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words = new[] { all[^1] };
        var kinds = all[..^1];
        bool Has(string? s) => s is not null && words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
        var food = NonFood.Any(n => q.Contains(n)) ? list : list.Where(o => !NonFood.Any(n => o.Title.Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
        var inTitle = food.Where(o => Has(o.Title)).ToList();
        var hits = inTitle.Count >= 3 ? inTitle : food.Where(o => Has(o.Title + " " + o.Description)).ToList();
        if (hits.Count == 0) return food.Count > 0 ? food : list;
        var marked = hits.Select(o => o with
        {
            Variant = IsVariant(o.Title, words),
            Matches = kinds.All(k => Synonyms(k).Any(v => (o.Title + " " + o.Description).Contains(v, StringComparison.OrdinalIgnoreCase))),
        }).ToList();
        return marked.OrderBy(o => o.Matches ? 0 : 1).ThenBy(o => o.Variant ? 1 : 0).ToList();   // stable: per-kg order kept within
    }

    // How the leaflets write a describing word: "øko", "Ø-mærket", "Lactofree".
    public static string[] Synonyms(string word) => word switch
    {
        "økologisk" or "økologiske" or "øko" => ["økologisk", "øko", "ø-mærke"],
        "laktosefri" => ["laktosefri", "lactofree", "laktosefrie"],
        "dansk" or "danske" => ["dansk", "danmark"],
        _ => [word.Length > 5 ? word[..^1] : word],   // "hakket"/"hakkede", "grov"/"grove": ignore the last letter
    };

    public static readonly string[] NonFood =
    [
        "shower", "showergel", "shampoo", "balsam", "sæbe", "bodylotion", "lotion", "creme", "deodorant", "deo ", "tandpasta",
        "vaskemiddel", "opvask", "skyllemiddel", "rengøring", "bleer", "vatrondel", "barbér", "hårfarve", "solcreme", "kattemad", "hundemad",
    ];

    // A flavour glued onto the searched word ("kakaomælk", "kakao-skummetmælk", "jordbæryoghurt") is another product than
    // the word itself. Kinds of the thing itself (skummetmælk, minimælk, letmælk) are not flavours and stay main.
    public static readonly string[] Flavours =
        ["kakao", "choko", "chokolade", "jordbær", "vanilje", "banan", "hindbær", "kaffe", "karamel", "mokka", "milkshake", "is "];

    public static bool IsVariant(string title, string[] words)
    {
        var t = title.ToLowerInvariant();
        if (words.Any(w => Flavours.Any(f => w.Contains(f.Trim())))) return false;   // searched for the flavour itself
        foreach (var w in words)
        {
            var hitPlain = false;
            for (var i = t.IndexOf(w, StringComparison.Ordinal); i >= 0; i = t.IndexOf(w, i + 1, StringComparison.Ordinal))
            {
                var start = i;
                while (start > 0 && (char.IsLetter(t[start - 1]) || t[start - 1] == '-')) start--;
                if (!Flavours.Any(f => t[start..i].Contains(f.Trim()))) hitPlain = true;
            }
            if (hitPlain) return false;
        }
        return true;
    }
}
