using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Server.Services;

// "Hent nyeste data": fetches loadouts and weapon stats from the Siege wiki (MediaWiki API) and the
// official ban rates, then computes weapon tiers, operator tiers, strengths/weaknesses, synergies and
// bans from the numbers. No AI and no paid calls - the user chose free, statistics-based data
// (2026-09-23) after a researched AI run cost $2.22 for a single call.
public class R6RefreshService
{
    private const string WikiApi = "https://rainbowsix.fandom.com/api.php";
    private const string BanRatesUrl = "https://www.esportstales.com/rainbow-six-siege/most-picked-and-banned-operators";
    private const int MustBanRate = 30;
    private const int TitlesPerRequest = 20;

    // Our operator names that the wiki spells differently.
    private static readonly Dictionary<string, string> WikiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nokk"] = "Nøkk", ["Skopos"] = "Skopós", ["Jager"] = "Jäger",
    };

    private readonly IHttpClientFactory _http;
    private readonly IR6DataService _data;
    private readonly ILogger<R6RefreshService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public R6RefreshService(IHttpClientFactory http, IR6DataService data, ILogger<R6RefreshService> log)
    {
        _http = http;
        _data = data;
        _log = log;
    }

    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "";
    public event Action? OnStatusChanged;

    private void Report(string status)
    {
        Status = status;
        _log.LogInformation("R6 refresh: {Status}", status);
        OnStatusChanged?.Invoke();
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct)) return;
        IsRunning = true;
        try
        {
            var data = await _data.GetData();
            var changes = new List<string>();
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("R6Assistant/1.0 (team tool; itmartin.dk)");
            client.Timeout = TimeSpan.FromSeconds(30);

            Report("Henter operatørernes loadouts fra wikien ...");
            var wikiOps = await FetchOperatorsAsync(client, data.Operators.Select(o => o.Name).ToList(), ct);
            foreach (var op in data.Operators)
            {
                if (!wikiOps.TryGetValue(op.Name, out var w)) continue;
                var before = OpSnapshot(op);
                op.Armor = w.Armor; op.Speed = w.Speed; op.Primaries = w.Primaries; op.Secondaries = w.Secondaries;
                op.Gadgets = w.Gadgets; op.Ability = w.Ability;
                var after = OpSnapshot(op);
                if (before != after && before != OpSnapshot(new R6Operator())) changes.Add($"{op.Name}: loadout ændret ({after})");
            }

            Report("Henter våben-stats fra wikien ...");
            var weaponNames = data.Operators.SelectMany(o => o.Primaries.Concat(o.Secondaries)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var weapons = await FetchWeaponsAsync(client, weaponNames, ct);
            var oldWeapons = data.Weapons.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var w in weapons)
            {
                w.Users = data.Operators.Where(o => o.Primaries.Concat(o.Secondaries).Contains(w.Name, StringComparer.OrdinalIgnoreCase)).Select(o => o.Name).ToList();
                if (oldWeapons.TryGetValue(w.Name, out var old))
                {
                    if (old.Damage != w.Damage) changes.Add($"{w.Name}: skade {old.Damage} → {w.Damage}");
                    if (old.Rpm != w.Rpm) changes.Add($"{w.Name}: skudhastighed {old.Rpm} → {w.Rpm} RPM");
                    if (old.Magazine != w.Magazine) changes.Add($"{w.Name}: magasin {old.Magazine} → {w.Magazine}");
                }
            }
            if (weapons.Count > 0) data.Weapons = weapons;

            Report("Henter officielle ban-rater ...");
            var bans = await FetchBanRatesAsync(client, ct);
            if (bans.Count >= 5)
            {
                foreach (var (name, rate) in bans)
                    if (data.BanRates.TryGetValue(name, out var was) && was != rate) changes.Add($"{name}: ban-rate {was} % → {rate} %");
                data.BanRates = bans;
            }

            Report("Beregner tiers, styrker/svagheder og bans ...");
            ComputeWeapons(data.Weapons);
            ComputeOperatorTiers(data);
            RecomputeBans(data);

            data.LastChanges = changes.Count > 0 ? changes : new() { "Ingen ændringer siden sidste hentning." };
            data.UpdatedAtUtc = DateTimeOffset.UtcNow;
            data.Sources = new() { "https://rainbowsix.fandom.com (loadouts og våben-stats)", BanRatesUrl + " (Ubisofts ban-rater)" };
            await _data.SaveAsync(data);

            Report($"Færdig: {wikiOps.Count} operatører, {data.Weapons.Count} våben, {bans.Count} ban-rater. {changes.Count} ændringer siden sidst.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "R6 refresh failed");
            Report($"Fejl: {ex.Message}. Intet er ændret.");
        }
        finally
        {
            IsRunning = false;
            OnStatusChanged?.Invoke();
            _gate.Release();
        }
    }

    private static string OpSnapshot(R6Operator o) =>
        $"{o.Armor}-armor/{o.Speed}-speed, {string.Join("/", o.Primaries)} + {string.Join("/", o.Secondaries)}, {string.Join("/", o.Gadgets)}";

    // ---------- wiki ----------

    private sealed record WikiOperator(int Armor, int Speed, List<string> Primaries, List<string> Secondaries, List<string> Gadgets, string Ability);

    private async Task<Dictionary<string, WikiOperator>> FetchOperatorsAsync(HttpClient client, List<string> names, CancellationToken ct)
    {
        var result = new Dictionary<string, WikiOperator>(StringComparer.OrdinalIgnoreCase);
        var wikiName = names.ToDictionary(n => n, n => WikiNames.TryGetValue(n, out var w) ? w : n, StringComparer.OrdinalIgnoreCase);

        // Siege operators live at "Name (Siege)"; a few only exist as "Name".
        var pages = await FetchPagesAsync(client, wikiName.Values.Select(w => w + " (Siege)"), ct);
        var missing = wikiName.Where(kv => !pages.ContainsKey(kv.Value + " (Siege)")).Select(kv => kv.Value).ToList();
        foreach (var kv in await FetchPagesAsync(client, missing, ct)) pages[kv.Key] = kv.Value;

        foreach (var (ours, wiki) in wikiName)
        {
            if (!pages.TryGetValue(wiki + " (Siege)", out var text) && !pages.TryGetValue(wiki, out text)) continue;
            var primaries = LongestList(text, "primary");
            if (primaries.Count == 0) continue;
            result[ours] = new WikiOperator(
                CountDots(Field(text, "armor")), CountDots(Field(text, "speed")),
                primaries, LongestList(text, "secondary"), LongestList(text, "gadget"), Ability(text));
        }
        _log.LogInformation("R6 refresh: wiki operators {Found}/{Total}", result.Count, names.Count);
        return result;
    }

    private async Task<List<R6Weapon>> FetchWeaponsAsync(HttpClient client, List<string> names, CancellationToken ct)
    {
        var pages = await FetchPagesAsync(client, names, ct);
        var list = new List<R6Weapon>();
        foreach (var name in names)
        {
            if (!pages.TryGetValue(name, out var text)) continue;
            var siege = SiegeTab(text);
            var w = new R6Weapon
            {
                Name = name,
                Type = WeaponType(Field(siege, "type")),
                Damage = FirstInt(Field(siege, "damage per hit")) ?? FirstInt(Field(siege, "damage")) ?? 0,
                Rpm = FirstInt(Field(siege, "rate of fire")) ?? 0,
                Mobility = FirstInt(Field(siege, "mobility")) ?? 0,
                Magazine = FirstInt(Field(siege, "magazine")) ?? 0,
                ReloadTactical = Tactical(Field(siege, "reloadtime")),
            };
            if (w.Damage > 0) list.Add(w);
        }
        _log.LogInformation("R6 refresh: wiki weapons {Found}/{Total}", list.Count, names.Count);
        return list;
    }

    // Batched MediaWiki query: up to 50 titles per request, following redirects. Keys are the requested titles.
    private async Task<Dictionary<string, string>> FetchPagesAsync(HttpClient client, IEnumerable<string> titles, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in titles.Distinct().Chunk(TitlesPerRequest))
        {
            // requested title -> final title, through normalisation and redirects
            var map = chunk.ToDictionary(t => t, t => t);
            var content = new Dictionary<string, string>();
            // Big pages overflow the API's response size limit; the rest arrives through "continue".
            var cont = "";
            for (var round = 0; round < 20; round++)
            {
                var url = $"{WikiApi}?action=query&prop=revisions&rvprop=content&rvslots=main&redirects=1&format=json&formatversion=2&titles={Uri.EscapeDataString(string.Join("|", chunk))}{cont}";
                using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
                var q = doc.RootElement.GetProperty("query");
                foreach (var step in new[] { "normalized", "redirects" })
                    if (q.TryGetProperty(step, out var arr))
                        foreach (var e in arr.EnumerateArray())
                        {
                            var from = e.GetProperty("from").GetString()!; var to = e.GetProperty("to").GetString()!;
                            foreach (var k in map.Keys.ToList()) if (map[k] == from) map[k] = to;
                        }
                foreach (var p in q.GetProperty("pages").EnumerateArray())
                    if (p.TryGetProperty("revisions", out var revs))
                        content[p.GetProperty("title").GetString()!] = revs[0].GetProperty("slots").GetProperty("main").GetProperty("content").GetString() ?? "";
                if (!doc.RootElement.TryGetProperty("continue", out var next)) break;
                cont = string.Concat(next.EnumerateObject().Select(pr => $"&{pr.Name}={Uri.EscapeDataString(pr.Value.ToString())}"));
            }
            foreach (var (req, final) in map)
                if (content.TryGetValue(final, out var c) && !c.StartsWith("#REDIRECT", StringComparison.OrdinalIgnoreCase)) result[req] = c;
        }
        return result;
    }

    private static string SiegeTab(string text)
    {
        var i = text.IndexOf("\nSiege=", StringComparison.Ordinal);
        if (i < 0) return text;
        var end = text.IndexOf("\n|-|", i + 1, StringComparison.Ordinal);
        var tabEnd = text.IndexOf("</tabber>", i + 1, StringComparison.Ordinal);
        if (end < 0 || (tabEnd >= 0 && tabEnd < end)) end = tabEnd;
        return end < 0 ? text[i..] : text[i..end];
    }

    // "|key = value" up to the next field or the end of the template.
    private static string Field(string text, string key)
    {
        var m = Regex.Match(text, @"\|\s*" + Regex.Escape(key) + @"\s*=(.*?)(?=\n\s*\||\}\}\s*\n|\}\}$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    // The infobox and the Loadout template both carry e.g. "primary"; the Loadout one lists every option.
    // Newer pages use a wikitable instead: "|Primary" on its own line, bullets below, "|-" ends the row.
    private static List<string> LongestList(string text, string key)
    {
        var best = new List<string>();
        var template = Regex.Matches(text, @"\|\s*" + key + @"\s*=(.*?)(?=\n\s*\||\}\})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var table = Regex.Matches(text, @"\n\|\s*" + key + @"\s*\n(.*?)(?=\n\|-|\n\|\})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match m in template.Concat(table))
        {
            var items = m.Groups[1].Value.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith('*') || l.StartsWith("[[")).Select(Clean).Where(s => s.Length > 0).ToList();
            if (items.Count > best.Count) best = items;
        }
        return best;
    }

    // Strips wiki markup: [[Target|Shown]] -> Shown, {{Color|#x|Text}} -> Text, tags, bullets, HTML entities.
    private static string Clean(string s)
    {
        s = Regex.Replace(s, @"\[\[File:[^\]]*\]\]", "");
        s = Regex.Replace(s, @"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]", "$1");
        s = Regex.Replace(s, @"\{\{Color\|[^|}]*\|([^}]*)\}\}", "$1");
        s = Regex.Replace(s, @"<small[^>]*>.*?</small>", "", RegexOptions.Singleline);
        s = Regex.Replace(s, @"<br\s*/?>", " ");
        s = Regex.Replace(s, @"<[^>]+>|\{\{[^}]*\}\}", "");
        s = WebUtility.HtmlDecode(s).TrimStart('*').Trim();
        return Regex.Replace(s, @"\s+", " ");
    }

    private static string Ability(string text)
    {
        var field = Clean(Field(text, "ability"));
        if (field.Length > 0) return field;
        var m = Regex.Match(text, @"\n\|\s*Ability\s*\n(.*?)(?=\n\|-|\n\|\})", RegexOptions.Singleline);
        return m.Success ? Clean(m.Groups[1].Value) : "";
    }

    // "Revolver|appearances=..." (one-line infoboxes) -> "Revolver"; "Pistol" is the same class as "Handgun".
    private static string WeaponType(string raw)
    {
        var t = Clean(raw.Split('|')[0]);
        return t.Equals("Pistol", StringComparison.OrdinalIgnoreCase) ? "Handgun" : t;
    }

    private static int CountDots(string s) => Math.Clamp(Regex.Matches(s, "&#9679;|●").Count, 0, 3);

    private static int? FirstInt(string s)
    {
        var m = Regex.Match(Regex.Replace(s, @"\{\{WeaponDamage\|[A-Za-z]+\|", ""), @"\d+");
        return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : null;
    }

    private static double Tactical(string s)
    {
        var m = Regex.Match(s, @"([\d.]+)\s*s\s*\(Tactical\)", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(s, @"([\d.]+)\s*s");
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private async Task<Dictionary<string, int>> FetchBanRatesAsync(HttpClient client, CancellationToken ct)
    {
        var rates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var html = await client.GetStringAsync(BanRatesUrl, ct);
            foreach (Match row in Regex.Matches(html, @"<tr.*?</tr>", RegexOptions.Singleline))
            {
                var cells = Regex.Matches(row.Value, @"<t[hd][^>]*>(.*?)</t[hd]>", RegexOptions.Singleline)
                    .Select(c => WebUtility.HtmlDecode(Regex.Replace(c.Groups[1].Value, "<[^>]+>", "")).Trim()).ToList();
                if (cells.Count >= 3 && Regex.Match(cells[2], @"^(\d+)\s*%$") is { Success: true } m)
                    rates[cells[1]] = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "R6 refresh: ban rates not fetched"); }
        return rates;
    }

    // ---------- computed from the numbers ----------

    private const int ThreeArmorHp = 125;

    public static void ComputeWeapons(List<R6Weapon> weapons)
    {
        foreach (var w in weapons)
        {
            var shotgun = w.Type.Contains("Shotgun", StringComparison.OrdinalIgnoreCase);
            w.Dps = w.Rpm > 0 ? w.Damage * w.Rpm / 60 : 0;
            w.TtkMs = !shotgun && w.Rpm > 0 && w.Damage > 0 ? (int)Math.Round((Math.Ceiling(ThreeArmorHp / (double)w.Damage) - 1) * 60000.0 / w.Rpm) : 0;
        }
        foreach (var group in weapons.GroupBy(w => w.Type))
        {
            var g = group.ToList();
            // Rank by time to kill (lower is better); shotguns and weapons without a fire rate fall back to damage.
            var ranked = g.OrderBy(w => w.TtkMs > 0 ? w.TtkMs : int.MaxValue).ThenByDescending(w => w.Dps).ThenByDescending(w => w.Damage).ToList();
            for (var i = 0; i < ranked.Count; i++)
                ranked[i].Tier = ranked.Count < 2 ? "B" : (i * 4 / ranked.Count) switch { 0 => "S", 1 => "A", 2 => "B", _ => "C" };

            foreach (var w in g)
            {
                w.Strengths = new(); w.Weaknesses = new();
                void Rel(Func<R6Weapon, double> f, string high, string low, bool lowerIsBetter = false)
                {
                    var vals = g.Select(f).Where(v => v > 0).OrderBy(v => v).ToList();
                    var v = f(w);
                    if (vals.Count < 3 || v <= 0) return;
                    var top = vals[(int)(vals.Count * 0.75)]; var bottom = vals[(int)(vals.Count * 0.25)];
                    var good = lowerIsBetter ? v <= bottom : v >= top;
                    var bad = lowerIsBetter ? v >= top : v <= bottom;
                    if (good) w.Strengths.Add(high); else if (bad) w.Weaknesses.Add(low);
                }
                Rel(x => x.TtkMs, $"Dræber blandt de hurtigste i klassen ({w.TtkMs} ms mod 3-armor).", $"Dræber langsommere end de fleste i klassen ({w.TtkMs} ms mod 3-armor).", lowerIsBetter: true);
                Rel(x => x.Damage, $"Høj skade pr. skud ({w.Damage}).", $"Lav skade pr. skud ({w.Damage}) - der skal flere træffere til.");
                Rel(x => x.Rpm, $"Høj skudhastighed ({w.Rpm} RPM).", $"Lav skudhastighed ({w.Rpm} RPM) - hvert skud skal sidde.");
                Rel(x => x.Magazine, $"Stort magasin ({w.Magazine}).", $"Lille magasin ({w.Magazine}) - du genlader tit.");
                Rel(x => x.ReloadTactical, $"Hurtig genladning ({w.ReloadTactical:0.0} s).", $"Langsom genladning ({w.ReloadTactical:0.0} s).", lowerIsBetter: true);
                Rel(x => x.Mobility, "Let våben - hurtig at bevæge sig og sigte med.", "Tungt våben - langsommere at bevæge sig og sigte med.");
            }
        }
    }

    private static readonly Dictionary<string, int> TierPoints = new() { ["S"] = 3, ["A"] = 2, ["B"] = 1, ["C"] = 0 };

    // Tier per side from: official ban rate, the best primary weapon's tier and how many sites list the
    // operator as a typical pick. Presence/win rate for every operator is only published as a chart image,
    // so it is not part of the score.
    public static void ComputeOperatorTiers(R6GameData data)
    {
        var weapons = data.Weapons.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
        var sites = data.Maps.Where(m => m.IsRanked).SelectMany(m => m.Sites).ToList();
        int SiteCount(R6Operator o) => sites.Count(s => (o.Side == "Attack" ? s.AttackPicks : s.DefensePicks).Contains(o.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var side in data.Operators.GroupBy(o => o.Side))
        {
            var maxSites = Math.Max(1, side.Max(SiteCount));
            var scored = side.Select(o =>
            {
                var ban = data.BanRates.TryGetValue(o.Name, out var r) ? r : 0;
                var best = o.Primaries.Select(p => weapons.GetValueOrDefault(p)).Where(w => w is not null)
                    .OrderByDescending(w => TierPoints.GetValueOrDefault(w!.Tier)).FirstOrDefault();
                var n = SiteCount(o);
                var score = Math.Min(ban, 60) / 10.0 + (best is null ? 1 : TierPoints.GetValueOrDefault(best.Tier)) + 3.0 * n / maxSites;
                var why = new List<string>();
                if (ban > 0) why.Add($"Bannes i {ban} % af kampene.");
                if (best is not null) why.Add($"Bedste våben: {best.Name} (tier {best.Tier} blandt {best.Type.ToLowerInvariant()}s).");
                why.Add(n > 0 ? $"Typisk pick på {n} af {sites.Count} ranked sites." : "Ikke et typisk pick på nogen ranked site.");
                return (o, score, why);
            }).OrderByDescending(x => x.score).ToList();
            for (var i = 0; i < scored.Count; i++)
            {
                scored[i].o.Tier = (i * 4 / scored.Count) switch { 0 => "S", 1 => "A", 2 => "B", _ => "C" };
                scored[i].o.TierWhy = scored[i].why;
            }
        }
        data.Tiers = data.Operators.Where(o => o.Tier.Length > 0).ToDictionary(o => o.Name, o => o.Tier, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, int> TierRank = new(StringComparer.OrdinalIgnoreCase)
        { ["S"] = 4, ["A"] = 3, ["B"] = 2, ["C"] = 1 };

    private static readonly Dictionary<int, string> TierText = new()
    {
        [4] = "Et af sæsonens stærkeste valg (tier S).",
        [3] = "Stærkt valg i denne sæson (tier A).",
        [2] = "Solidt valg i denne sæson (tier B).",
        [1] = "Sjældent valg i denne sæson.",
    };

    // 10 bans per side per site: "SKAL bannes" first (official ban rate >= 30 %), then the site's own
    // typical picks, then the rest of the roster by ban rate and tier. Every ban gets a reason.
    public static void RecomputeBans(R6GameData data)
    {
        int Rate(string n) => data.BanRates.TryGetValue(n, out var r) ? r : 0;
        int Tier(string n) => data.Tiers.TryGetValue(n, out var t) && TierRank.TryGetValue(t, out var r) ? r : 0;
        bool Must(string n) => Rate(n) >= MustBanRate;
        IEnumerable<string> Ranked(IEnumerable<string> names) =>
            names.OrderByDescending(Must).ThenByDescending(Rate).ThenByDescending(Tier);

        var bySide = data.Operators.GroupBy(o => o.Side).ToDictionary(g => g.Key, g => g.Select(o => o.Name).ToList());
        var threat = data.Operators.ToDictionary(o => o.Name, o => o.Threat, StringComparer.OrdinalIgnoreCase);

        foreach (var site in data.Maps.SelectMany(m => m.Sites))
        {
            var bans = new List<string>();
            var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (side, picks) in new[] { ("Defense", site.DefensePicks), ("Attack", site.AttackPicks) })
            {
                if (!bySide.TryGetValue(side, out var roster)) continue;
                var top = roster.Where(Must)
                    .Concat(Ranked(picks.Where(p => roster.Contains(p) && !Must(p))))
                    .Concat(Ranked(roster.Where(n => !Must(n) && !picks.Contains(n))))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10);
                foreach (var name in top)
                {
                    var parts = new List<string>();
                    if (Rate(name) > 0) parts.Add($"Bannes i {Rate(name)} % af kampene.");
                    if (picks.Contains(name)) parts.Add("Bliver tit spillet på dette site.");
                    if (threat.TryGetValue(name, out var t) && t.Length > 0) parts.Add(t);
                    if (Rate(name) == 0 && !picks.Contains(name) && TierText.TryGetValue(Tier(name), out var tt)) parts.Add(tt);
                    reasons[name] = string.Join(" ", parts);
                    bans.Add(name);
                }
            }
            site.SuggestedBans = bans;
            site.MustBans = bans.Where(Must).ToList();
            site.BanReasons = reasons;
        }
    }

    // Partners = operators on the same side that are typical picks on the same sites.
    public static List<(string Name, int Sites)> Partners(R6GameData data, R6Operator op, int take = 4)
    {
        var picks = data.Maps.Where(m => m.IsRanked).SelectMany(m => m.Sites)
            .Select(s => op.Side == "Attack" ? s.AttackPicks : s.DefensePicks)
            .Where(p => p.Contains(op.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        return picks.SelectMany(p => p).Where(n => !n.Equals(op.Name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2).Take(take).ToList();
    }
}
