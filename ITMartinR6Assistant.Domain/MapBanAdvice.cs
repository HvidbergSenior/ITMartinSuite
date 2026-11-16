namespace ITMartinR6Assistant.Domain;

// One operator to ban on a map, with the number behind it.
public record BanSuggestion(string Operator, double Score, string Label, bool FromPro);

// Who to ban on a map, for one side (user 2026-11-11: Oregon showed "Ingen pro-data" and nothing to ban).
// Pro league ban % when Liquipedia has the map; otherwise the map's own site bans (each site lists 10, MustBans first):
// an operator suggested on more sites, or marked "SKAL bannes", ranks higher.
public static class MapBanAdvice
{
    public static List<BanSuggestion> For(R6Map map, ProMapStats? pro, ISet<string> sideOperators, int take = 6)
    {
        if (pro is not null && pro.OperatorBanPct.Count > 0)
        {
            var fromPro = pro.OperatorBanPct.Where(kv => sideOperators.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value).Take(take)
                .Select(kv => new BanSuggestion(kv.Key, kv.Value, kv.Value.ToString("0") + " %", true)).ToList();
            if (fromPro.Count > 0) return fromPro;
        }

        var siteCount = map.Sites.Count;
        return map.Sites
            .SelectMany(s => s.SuggestedBans.Take(5).Where(sideOperators.Contains)
                .Select(n => (Name: n, Points: s.MustBans.Contains(n, StringComparer.OrdinalIgnoreCase) ? 2.0 : 1.0)))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.Key, Sites: g.Count(), Points: g.Sum(x => x.Points)))
            .OrderByDescending(x => x.Points).ThenBy(x => x.Name).Take(take)
            .Select(x => new BanSuggestion(x.Name, x.Points, $"{x.Sites} af {siteCount} sites", false))
            .ToList();
    }
}
