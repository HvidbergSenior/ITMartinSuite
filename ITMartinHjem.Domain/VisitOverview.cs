namespace ITMartinHjem.Domain;

/// <summary>/admin/tal in plain words (user 2026-11-13: "more easy to understand... How many but also how many the same user"
/// + "I still do not know if some of them are myself"). One "person" = one browser on one site/app (the id lives in that
/// site's own storage), so the same human in three apps can be three persons - the page says so.</summary>
public static class VisitOverview
{
    /// <param name="Label">Anonymous: "Person 1", "Person 2" ... (no names, no IPs).</param>
    /// <param name="LikelyOwner">Opened a page only Martin uses (studio, admin, login) - probably himself.</param>
    public sealed record Person(string Key, string Label, int DaysInPeriod, int DaysEver, int Pages, string Device,
        IReadOnlyList<string> Hosts, DateTime FirstEver, DateTime Last, bool LikelyOwner)
    {
        public bool CanMark => !Key.StartsWith('#');   // hits without a browser id cannot be marked
    }

    public sealed record Summary(int People, int New, int Returning, int Visits, int Pages, int Loyal, IReadOnlyList<Person> Top);

    public const int LoyalDays = 3, TopCount = 15;

    // Paths that only Martin opens: a person who has been there is very likely him.
    private static readonly string[] OwnerPaths = ["/studio", "/admin", "/login", "/svar", "/ret"];

    public static bool IsOwnerPath(string path) =>
        OwnerPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith(p + "?", StringComparison.OrdinalIgnoreCase));

    /// <param name="all">Every stored hit (the history decides who is new and who came back).</param>
    /// <param name="sinceUtc">Start of the chosen period.</param>
    /// <param name="name">Short name for a host (fx "Stem").</param>
    public static Summary Build(IReadOnlyCollection<Hit> all, DateTime sinceUtc, Func<string, string> name)
    {
        var byPerson = all.GroupBy(h => h.VisitorKey).ToList();
        var people = new List<Person>();
        int visits = 0, pages = 0, fresh = 0, back = 0, loyal = 0;
        foreach (var g in byPerson)
        {
            var inPeriod = g.Where(h => h.At >= sinceUtc).ToList();
            if (inPeriod.Count == 0) continue;
            var daysIn = inPeriod.Select(h => Dk.Local(h.At).Date).Distinct().Count();
            var daysEver = g.Select(h => Dk.Local(h.At).Date).Distinct().Count();
            var first = g.Min(h => h.At);
            visits += daysIn; pages += inPeriod.Count;
            if (first >= sinceUtc && daysEver == daysIn && daysIn == 1) fresh++;
            if (daysEver >= 2) back++;
            if (daysIn >= LoyalDays) loyal++;
            people.Add(new Person(g.Key, "", daysIn, daysEver, inPeriod.Count,
                inPeriod.GroupBy(h => h.Device).OrderByDescending(d => d.Count()).First().Key,
                inPeriod.GroupBy(h => h.Host).OrderByDescending(x => x.Count()).Select(x => name(x.Key)).Take(3).ToList(),
                first, inPeriod.Max(h => h.At), g.Any(h => IsOwnerPath(h.Path))));
        }
        var top = people.Where(p => p.DaysEver >= 2 || p.LikelyOwner)
            .OrderByDescending(p => p.LikelyOwner).ThenByDescending(p => p.DaysInPeriod).ThenByDescending(p => p.Pages)
            .Take(TopCount).Select((p, i) => p with { Label = $"Person {i + 1}" }).ToList();
        return new Summary(people.Count, fresh, back, visits, pages, loyal, top);
    }
}
