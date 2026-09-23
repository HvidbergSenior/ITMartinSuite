using System.Globalization;
using System.Text;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Server.Services;

// "Hent nyeste data": everything comes from Liquipedia's free MediaWiki API (user, 2026-09-23: "everything should be
// taken from that api", pro data from the last two years). Operators and weapons come from their infobox pages; pro
// numbers (ban rates, bans per map, map picks/bans, rounds per side, recent matches) are computed from the match
// records on every S-tier event page. No AI and no paid calls. Liquipedia's API terms: a custom User-Agent, gzip
// and at most one request every 2 seconds - a full run is ~30 requests, so about a minute.
public class R6RefreshService
{
    private const string Api = "https://liquipedia.net/rainbowsix/api.php";
    private const string Site = "https://liquipedia.net/rainbowsix/";
    public const int MustBanRate = 30;
    private const int TitlesPerRequest = 20;
    private static readonly TimeSpan RequestGap = TimeSpan.FromSeconds(2.1);
    private static readonly Regex SkipEvent = new(@"Show ?[Mm]atch|Awards|Standings|Qualifier|Road to|Statistics", RegexOptions.IgnoreCase);

    private readonly IHttpClientFactory _http;
    private readonly IR6DataService _data;
    private readonly ILogger<R6RefreshService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRequest = DateTime.MinValue;

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
            var client = _http.CreateClient("liquipedia");

            Report("Henter operatører fra Liquipedia ...");
            var lpOps = await FetchOperatorsAsync(client, data.Operators.Select(o => o.Name).ToList(), ct);
            foreach (var op in data.Operators)
            {
                if (!lpOps.TryGetValue(op.Name, out var w)) continue;
                var before = OpSnapshot(op);
                op.Armor = w.Armor; op.Speed = w.Speed; op.Primaries = w.Primaries; op.Secondaries = w.Secondaries;
                op.Gadgets = w.Gadgets; op.Ability = w.Ability;
                op.CounteredBy = w.CounteredBy.Select(c => data.Operators.FirstOrDefault(o => Norm(o.Name) == Norm(c))?.Name ?? c).ToList();
                var after = OpSnapshot(op);
                if (before != after && before != OpSnapshot(new R6Operator())) changes.Add($"{op.Name}: loadout ændret ({after})");
            }

            Report("Henter våben fra Liquipedia ...");
            var weaponNames = data.Operators.SelectMany(o => o.Primaries.Concat(o.Secondaries)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var weapons = await FetchWeaponsAsync(client, weaponNames, ct);
            var oldWeapons = data.Weapons.ToDictionary(w => w.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var w in weapons)
            {
                w.Users = data.Operators.Where(o => o.Primaries.Concat(o.Secondaries).Contains(w.Name, StringComparer.OrdinalIgnoreCase)).Select(o => o.Name).ToList();
                if (!oldWeapons.TryGetValue(w.Name, out var old)) continue;
                w.ReloadTactical = old.ReloadTactical; // Liquipedia has no reload times - keep the last known value
                if (old.Damage != w.Damage) changes.Add($"{w.Name}: skade {old.Damage} → {w.Damage}");
                if (old.Rpm != w.Rpm) changes.Add($"{w.Name}: skudhastighed {old.Rpm} → {w.Rpm} RPM");
                if (old.Magazine != w.Magazine) changes.Add($"{w.Name}: magasin {old.Magazine} → {w.Magazine}");
            }
            if (weapons.Count > 0) data.Weapons = weapons;

            Report("Henter pro-kampe fra de sidste to år (Liquipedia) ...");
            var pro = await FetchProStatsAsync(client, data, ct);
            if (pro.MapsPlayed >= 50)
            {
                var bans = pro.OperatorBanPct.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value), StringComparer.OrdinalIgnoreCase);
                foreach (var (name, rate) in bans)
                    if (data.BanRates.TryGetValue(name, out var was) && Math.Abs(was - rate) >= 3) changes.Add($"{name}: pro ban-rate {was} % → {rate} %");
                data.BanRates = bans;
                data.Pro = pro;
            }
            else changes.Add($"Pro-data ikke opdateret: kun {pro.MapsPlayed} maps fundet.");

            Report("Beregner tiers, styrker/svagheder og bans ...");
            ComputeWeapons(data.Weapons);
            ComputeOperatorTiers(data);
            RecomputeBans(data);

            data.LastChanges = changes.Count > 0 ? changes : new() { "Ingen ændringer siden sidste hentning." };
            data.UpdatedAtUtc = DateTimeOffset.UtcNow;
            data.Sources = new() { Site + " (operatører, våben og pro-kampe: " + string.Join(", ", data.Pro.Events) + ")" };
            await _data.SaveAsync(data);

            Report($"Færdig: {lpOps.Count} operatører, {data.Weapons.Count} våben, {data.Pro.Matches} pro-kampe ({data.Pro.MapsPlayed} maps). {changes.Count} ændringer siden sidst.");
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

    // Liquipedia spells some names without accents or in lower case ("Capitao", "grim"); compare without both.
    public static string Norm(string s) =>
        new string(s.Replace("ø", "o").Replace("Ø", "O").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Trim().ToLowerInvariant();

    // ---------- Liquipedia API ----------

    private async Task<JsonDocument> GetJsonAsync(HttpClient client, string query, CancellationToken ct)
    {
        var wait = _lastRequest + RequestGap - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _lastRequest = DateTime.UtcNow;
        return JsonDocument.Parse(await client.GetStringAsync($"{Api}?format=json&formatversion=2&{query}", ct));
    }

    private sealed record LpOperator(int Armor, int Speed, List<string> Primaries, List<string> Secondaries, List<string> Gadgets, string Ability, List<string> CounteredBy);

    private async Task<Dictionary<string, LpOperator>> FetchOperatorsAsync(HttpClient client, List<string> names, CancellationToken ct)
    {
        var result = new Dictionary<string, LpOperator>(StringComparer.OrdinalIgnoreCase);
        var pages = await FetchPagesAsync(client, names, ct);
        foreach (var name in names)
        {
            if (!pages.TryGetValue(name, out var text)) continue;
            var primaries = Section(text, "Primary");
            if (primaries.Count == 0) continue;
            var armor = FirstInt(Field(text, "armor")) ?? 0;
            var speed = Field(text, "speed").ToLowerInvariant() switch { "fast" => 3, "medium" or "normal" => 2, "slow" => 1, _ => 0 };
            var gadgets = Regex.Matches(Between(text, "==Equipment==", @"\n==[^=]"), @"\{\{equipments/([^}|]+)")
                .Select(m => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Groups[1].Value.Trim())).ToList();
            var card = Between(text, "{{GadgetCard", @"\n\}\}");
            var counters = Regex.Matches(card, @"\|countered\d*=([^|\n]+)").Select(m => m.Groups[1].Value.Trim()).Where(s => s.Length > 0).ToList();
            result[name] = new LpOperator(Math.Clamp(armor, 0, 3), speed, primaries, Section(text, "Secondary"),
                gadgets, Clean(Field(card, "name")), counters);
        }
        _log.LogInformation("R6 refresh: Liquipedia operators {Found}/{Total}", result.Count, names.Count);
        return result;
    }

    // Weapon names in "===Primary===" / "===Secondary===" up to the next heading.
    private static List<string> Section(string text, string heading) =>
        Regex.Matches(Between(text, "===" + heading + "===", @"\n=="), @"\{\{Weapon\|name=([^}|]+)")
            .Select(m => m.Groups[1].Value.Trim()).ToList();

    private static string Between(string text, string start, string endPattern)
    {
        var i = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        var rest = text[(i + start.Length)..];
        var m = Regex.Match(rest, endPattern);
        return m.Success ? rest[..m.Index] : rest;
    }

    private async Task<List<R6Weapon>> FetchWeaponsAsync(HttpClient client, List<string> names, CancellationToken ct)
    {
        var pages = await FetchPagesAsync(client, names, ct);
        var list = new List<R6Weapon>();
        foreach (var name in names)
        {
            if (!pages.TryGetValue(name, out var text)) continue;
            var w = new R6Weapon
            {
                Name = name,
                Type = WeaponType(Field(text, "class")),
                Damage = FirstInt(Field(text, "damage")) ?? 0,
                Rpm = FirstInt(Field(text, "rateoffire")) ?? 0,
                Mobility = FirstInt(Field(text, "mobility")) ?? 0,
                Magazine = FirstInt(Field(text, "magsize")) ?? 0,
            };
            if (w.Damage > 0) list.Add(w);
        }
        _log.LogInformation("R6 refresh: Liquipedia weapons {Found}/{Total}", list.Count, names.Count);
        return list;
    }

    // Batched MediaWiki query: up to 20 titles per request, following redirects. Keys are the requested titles.
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
            for (var round = 0; round < 30; round++)
            {
                using var doc = await GetJsonAsync(client, $"action=query&prop=revisions&rvprop=content&rvslots=main&redirects=1&titles={Uri.EscapeDataString(string.Join("|", chunk))}{cont}", ct);
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

    // "|key=value" up to the next field, line end or template end.
    private static string Field(string text, string key)
    {
        var m = Regex.Match(text, @"\|\s*" + Regex.Escape(key) + @"\s*=([^|\n}]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    // Strips wiki markup: [[Target|Shown]] -> Shown, tags, templates, bold/italic quotes, HTML entities.
    private static string Clean(string s)
    {
        s = Regex.Replace(s, @"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]", "$1");
        s = Regex.Replace(s, @"<[^>]+>|\{\{[^}]*\}\}|'''?", "");
        return Regex.Replace(WebUtility.HtmlDecode(s).Trim(), @"\s+", " ");
    }

    // "Submachine gun" -> "Submachine Gun"; "Pistol" is the same class as "Handgun".
    private static string WeaponType(string raw)
    {
        var t = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Clean(raw).ToLowerInvariant());
        return t is "Pistol" ? "Handgun" : t;
    }

    private static int? FirstInt(string s)
    {
        var m = Regex.Match(s, @"\d+");
        return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : null;
    }

    // ---------- pro matches ----------

    private async Task<ProStats> FetchProStatsAsync(HttpClient client, R6GameData data, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = today.AddYears(-2);
        var years = Enumerable.Range(since.Year, today.Year - since.Year + 1).Select(y => y.ToString(CultureInfo.InvariantCulture)).ToList();

        var events = new List<string>();
        using (var doc = await GetJsonAsync(client, "action=query&list=categorymembers&cmtitle=Category:S-Tier_Tournaments&cmlimit=500", ct))
            foreach (var m in doc.RootElement.GetProperty("query").GetProperty("categorymembers").EnumerateArray())
            {
                var title = m.GetProperty("title").GetString()!;
                if (!SkipEvent.IsMatch(title) && years.Any(title.Contains)) events.Add(title);
            }

        // Match records live on the event page and its sub-pages (group stage, playoffs, regions).
        var titles = new List<string>(events);
        foreach (var e in events)
        {
            using var doc = await GetJsonAsync(client, $"action=query&list=allpages&aplimit=100&apprefix={Uri.EscapeDataString(e + "/")}", ct);
            titles.AddRange(doc.RootElement.GetProperty("query").GetProperty("allpages").EnumerateArray()
                .Select(p => p.GetProperty("title").GetString()!).Where(t => !SkipEvent.IsMatch(t)));
        }
        var pages = await FetchPagesAsync(client, titles, ct);

        var mapNames = data.Maps.Select(m => m.Name).ToList();
        string OurMap(string lp) => mapNames.FirstOrDefault(n => Norm(lp) == Norm(n) || Norm(lp).StartsWith(Norm(n) + " ")) ?? lp.Trim();
        var opNames = data.Operators.GroupBy(o => Norm(o.Name)).ToDictionary(g => g.Key, g => g.First().Name);
        // Exact name first, then a unique prefix for short forms such as "Valk".
        string OurOp(string lp)
        {
            var k = Norm(lp);
            if (opNames.TryGetValue(k, out var n)) return n;
            var hits = opNames.Where(kv => k.Length >= 3 && kv.Key.StartsWith(k)).Select(kv => kv.Value).ToList();
            return hits.Count == 1 ? hits[0] : lp.Trim();
        }

        var pro = new ProStats { UpdatedAtUtc = DateTimeOffset.UtcNow, From = since, To = today };
        double totalWeight = 0;
        var opBan = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var opEarly = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var perMap = new Dictionary<string, MapAcc>(StringComparer.OrdinalIgnoreCase);
        MapAcc Acc(string map) => perMap.TryGetValue(map, out var a) ? a : perMap[map] = new MapAcc();
        var seenEvents = new HashSet<string>();
        DateOnly? first = null;

        foreach (var (title, text) in pages)
        {
            var eventName = events.Where(e => title == e || title.StartsWith(e + "/")).OrderByDescending(e => e.Length).FirstOrDefault() ?? title;
            foreach (var block in Regex.Split(text, @"\{\{Match\b").Skip(1))
            {
                var dm = Regex.Match(block, @"\|date=(\d{4}-\d{2}-\d{2})");
                if (!dm.Success || !DateOnly.TryParseExact(dm.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || date < since || date > today) continue;
                var maps = Regex.Matches(block, @"\{\{Map\|map=([^|\n}]+)\|finished=true(.*?)\}\}", RegexOptions.Singleline);
                if (maps.Count == 0) continue;
                // Recent events count more: the meta moves with every season.
                var months = (today.DayNumber - date.DayNumber) / 30.4;
                var weight = months <= 6 ? 3.0 : months <= 12 ? 2.0 : 1.0;
                pro.Matches++;
                seenEvents.Add(eventName);
                if (first is null || date < first) first = date;

                var t1 = TeamName(block, "opponent1"); var t2 = TeamName(block, "opponent2");
                var sgg = Regex.Match(block, @"\|siegegg=(\d+)").Groups[1].Value;
                var vod = Regex.Match(block, @"\|vod=(https?://[^|\s}]+)").Groups[1].Value;
                var url = vod.Length > 0 ? vod : sgg.Length > 0 ? $"https://siege.gg/matches/{sgg}" : Site + title.Replace(' ', '_');

                // Map veto: types=ban,ban,pick,ban,decider with t1mapN/t2mapN in that order.
                var veto = Regex.Match(block, @"\{\{MapVeto(.*?)\}\}", RegexOptions.Singleline).Groups[1].Value;
                if (veto.Length > 0)
                {
                    var types = Field(veto, "types").Split(',').Select(s => s.Trim()).ToList();
                    var vetoMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < types.Count; i++)
                    {
                        if (types[i] == "decider")
                        {
                            var d = Field(veto, "decider");
                            if (d.Any(char.IsLetter)) { var dmap = OurMap(d); Acc(dmap).Picks += weight; vetoMaps.Add(dmap); }
                            continue;
                        }
                        foreach (var team in new[] { "t1", "t2" })
                        {
                            var name = Field(veto, $"{team}map{i + 1}");
                            if (!name.Any(char.IsLetter)) continue; // "-" = no map
                            var vmap = OurMap(name); vetoMaps.Add(vmap);
                            if (types[i] == "ban") Acc(vmap).Bans += weight; else if (types[i] == "pick") Acc(vmap).Picks += weight;
                        }
                    }
                    foreach (var vm in vetoMaps) Acc(vm).Vetoes += weight;
                }

                foreach (Match mp in maps)
                {
                    var map = OurMap(mp.Groups[1].Value);
                    var f = mp.Groups[2].Value;
                    var acc = Acc(map);
                    pro.MapsPlayed++;
                    totalWeight += weight;
                    acc.Played++; acc.Weight += weight;

                    var banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var team in new[] { "t1", "t2" })
                        for (var n = 1; n <= 8; n++)
                        {
                            var b = Field(f, $"{team}ban{n}");
                            if (b.Length == 0) continue;
                            var op = OurOp(b);
                            if (banned.Add(op) && n <= 2) opEarly[op] = opEarly.GetValueOrDefault(op) + weight;
                        }
                    foreach (var op in banned)
                    {
                        opBan[op] = opBan.GetValueOrDefault(op) + weight;
                        acc.OpBans[op] = acc.OpBans.GetValueOrDefault(op) + weight;
                    }

                    int R(string k) => FirstInt(Field(f, k)) ?? 0;
                    var atk = R("t1atk") + R("t2atk"); var def = R("t1def") + R("t2def");
                    acc.AtkRounds += atk * weight; acc.Rounds += (atk + def) * weight; acc.RoundCount += atk + def;
                    var s1 = R("t1atk") + R("t1def") + R("t1otatk") + R("t1otdef");
                    var s2 = R("t2atk") + R("t2def") + R("t2otatk") + R("t2otdef");
                    var firstSide = Field(f, "t1firstside");
                    if (s1 != s2 && firstSide is "atk" or "def")
                    {
                        var defStarterWon = firstSide == "def" ? s1 > s2 : s2 > s1;
                        acc.DefStartGames += weight; if (defStarterWon) acc.DefStartWins += weight;
                    }
                    acc.Recent.Add(new ProMatchRef { Date = date, Event = eventName, Teams = $"{t1} vs {t2}", Score = $"{s1}-{s2}", Url = url });
                }
            }
        }

        pro.Events = seenEvents.OrderBy(e => e).ToList();
        pro.From = first ?? since;
        if (totalWeight <= 0) return pro;
        pro.OperatorBanPct = opBan.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => Math.Round(100 * kv.Value / totalWeight, 1), StringComparer.OrdinalIgnoreCase);
        pro.EarlyBanPct = opEarly.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => Math.Round(100 * kv.Value / totalWeight, 1), StringComparer.OrdinalIgnoreCase);
        foreach (var (map, a) in perMap)
        {
            if (a.Played == 0 && a.Vetoes == 0) continue;
            pro.Maps[map] = new ProMapStats
            {
                Played = a.Played,
                Vetoes = (int)Math.Round(a.Vetoes),
                PickPct = a.Vetoes > 0 ? Math.Round(100 * a.Picks / a.Vetoes, 1) : 0,
                BanPct = a.Vetoes > 0 ? Math.Round(100 * a.Bans / a.Vetoes, 1) : 0,
                Rounds = a.RoundCount,
                AtkRoundPct = a.Rounds > 0 ? Math.Round(100 * a.AtkRounds / a.Rounds, 1) : 0,
                DefStartWinPct = a.DefStartGames > 0 ? Math.Round(100 * a.DefStartWins / a.DefStartGames, 1) : 0,
                OperatorBanPct = a.Weight > 0
                    ? a.OpBans.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => Math.Round(100 * kv.Value / a.Weight, 1), StringComparer.OrdinalIgnoreCase)
                    : new(StringComparer.OrdinalIgnoreCase),
                RecentMatches = a.Recent.OrderByDescending(r => r.Date).Take(6).ToList(),
            };
        }
        return pro;
    }

    private sealed class MapAcc
    {
        public int Played, RoundCount;
        public double Weight, Vetoes, Picks, Bans, Rounds, AtkRounds, DefStartGames, DefStartWins;
        public Dictionary<string, double> OpBans { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ProMatchRef> Recent { get; } = new();
    }

    private static string TeamName(string block, string key)
    {
        var m = Regex.Match(block, @"\|" + key + @"=\{\{TeamOpponent\|([^}|]+)");
        return m.Success ? m.Groups[1].Value.Trim() : "?";
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

    // Tier = what the pro league does: share of pro maps where the operator is banned (Liquipedia, last two years,
    // recent events weighted). S >= 30 %, A >= 10 %, B >= 2 %, C below. Liquipedia stores bans, not picks, so an
    // operator the pros never ban lands in C even if people like playing it - the reasons say so.
    public static void ComputeOperatorTiers(R6GameData data)
    {
        var pro = data.Pro;
        foreach (var o in data.Operators)
        {
            var ban = pro.OperatorBanPct.GetValueOrDefault(o.Name);
            var early = pro.EarlyBanPct.GetValueOrDefault(o.Name);
            o.Tier = ban >= 30 ? "S" : ban >= 10 ? "A" : ban >= 2 ? "B" : "C";
            var why = new List<string>();
            if (pro.MapsPlayed == 0) { why.Add("Ingen pro-data hentet endnu - tryk \"Hent nyeste data\" under Rediger."); o.TierWhy = why; continue; }
            why.Add(ban > 0
                ? $"Pro league banner {o.Name} på {ban:0.#} % af alle maps ({pro.MapsPlayed} maps, {pro.Matches} kampe)."
                : $"Pro league har ikke bannet {o.Name} i de sidste to år ({pro.MapsPlayed} maps).");
            if (early >= 5) why.Add($"Pro league bruger en af de to første bans på {o.Name} i {early:0.#} % af maps - frygtet tidligt.");
            var topMaps = pro.Maps.Where(m => m.Value.Played >= 10 && m.Value.OperatorBanPct.GetValueOrDefault(o.Name) >= Math.Max(10, ban * 1.3))
                .OrderByDescending(m => m.Value.OperatorBanPct[o.Name]).Take(3)
                .Select(m => $"{m.Key} ({m.Value.OperatorBanPct[o.Name]:0} %)").ToList();
            if (topMaps.Count > 0) why.Add($"Pro league banner ham mest på: {string.Join(", ", topMaps)}.");
            if (o.CounteredBy.Count > 0) why.Add($"Kontres af: {string.Join(", ", o.CounteredBy)}.");
            o.TierWhy = why;
        }
        data.Tiers = data.Operators.Where(o => o.Tier.Length > 0).ToDictionary(o => o.Name, o => o.Tier, StringComparer.OrdinalIgnoreCase);
    }

    // 10 bans per side per site: "SKAL bannes" first (pro ban rate >= 30 %), then what the pro league bans on
    // this map, then the site's typical picks, then the rest by pro ban rate. Every ban gets a reason.
    public static void RecomputeBans(R6GameData data)
    {
        int Rate(string n) => data.BanRates.TryGetValue(n, out var r) ? r : 0;
        bool Must(string n) => Rate(n) >= MustBanRate;
        var bySide = data.Operators.GroupBy(o => o.Side).ToDictionary(g => g.Key, g => g.Select(o => o.Name).ToList());
        var threat = data.Operators.ToDictionary(o => o.Name, o => o.Threat, StringComparer.OrdinalIgnoreCase);

        foreach (var map in data.Maps)
        {
            var mapPro = data.Pro.Maps.GetValueOrDefault(map.Name);
            double MapRate(string n) => mapPro is { Played: >= 5 } ? mapPro.OperatorBanPct.GetValueOrDefault(n) : 0;
            foreach (var site in map.Sites)
            {
                var bans = new List<string>();
                var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (side, picks) in new[] { ("Defense", site.DefensePicks), ("Attack", site.AttackPicks) })
                {
                    if (!bySide.TryGetValue(side, out var roster)) continue;
                    var top = roster.Where(Must).OrderByDescending(MapRate).ThenByDescending(Rate)
                        .Concat(roster.Where(n => MapRate(n) >= 5).OrderByDescending(MapRate))
                        .Concat(picks.Where(roster.Contains).OrderByDescending(Rate))
                        .Concat(roster.OrderByDescending(Rate).ThenByDescending(MapRate))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(10);
                    foreach (var name in top)
                    {
                        var parts = new List<string>();
                        if (MapRate(name) > 0) parts.Add($"Pro league banner ham på {map.Name} i {MapRate(name):0} % af kampene.");
                        else if (Rate(name) > 0) parts.Add($"Pro league banner ham i {Rate(name)} % af alle kampe.");
                        if (picks.Contains(name)) parts.Add("Bliver tit spillet på dette site.");
                        if (threat.TryGetValue(name, out var t) && t.Length > 0) parts.Add(t);
                        if (parts.Count == 0) parts.Add("Sjældent bannet af pro league - kun med for at fylde listen.");
                        reasons[name] = string.Join(" ", parts);
                        bans.Add(name);
                    }
                }
                site.SuggestedBans = bans;
                site.MustBans = bans.Where(Must).ToList();
                site.BanReasons = reasons;
            }
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
