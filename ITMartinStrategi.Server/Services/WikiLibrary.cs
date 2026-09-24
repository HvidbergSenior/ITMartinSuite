using System.Globalization;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ITMartinStrategi.Server.Data;
using ITMartinStrategi.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStrategi.Server.Services;

/// <summary>
/// The free "look anything up" library: a game's MediaWiki pages (Game.Wiki) are pulled
/// through the public API, every wiki table row becomes its own entry (one artifact, one
/// technology …), and the rest of each page becomes a page entry. Rebuilt weekly in the
/// background or on demand. Search is local and forgiving, because the wikis' own search
/// is often broken (wiki.galciv.com finds nothing at all) and people misspell names.
/// </summary>
public sealed class WikiLibrary(IHttpClientFactory http, IDbContextFactory<StrategiDbContext> dbFactory, ILogger<WikiLibrary> log)
    : BackgroundService
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromDays(7);
    private readonly Dictionary<int, List<WikiEntry>> _cache = new();
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private readonly object _cacheLock = new();

    public bool Importing { get; private set; }
    public string Progress { get; private set; } = "";
    public event Action? Changed;

    // ---------------------------------------------------------------- refresh

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stop);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(stop);
                var due = await db.Games.Where(g => g.Wiki != "").ToListAsync(stop);
                foreach (var g in due.Where(g => g.WikiUpdatedAt is null || DateTime.UtcNow - g.WikiUpdatedAt > RefreshEvery))
                    await ImportAsync(g.Id, stop);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { log.LogWarning(ex, "Wiki refresh failed"); }
            await Task.Delay(TimeSpan.FromHours(6), stop);
        }
    }

    public async Task<int> ImportAsync(int gameId, CancellationToken ct = default)
    {
        if (!await _importLock.WaitAsync(0, ct)) return -1;   // one import at a time
        try
        {
            Importing = true; Progress = "Starting …"; Changed?.Invoke();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var game = await db.Games.FirstAsync(g => g.Id == gameId, ct);
            // "base|namespace" plus optional "|ns:text" extras: pages in another namespace whose
            // title contains the text (GalCiv IV keeps "Approval GC4", "GC4 Artifacts Table" … in main).
            var parts = game.Wiki.Split('|');
            if (parts.Length < 2) return 0;
            var (baseUrl, ns) = (parts[0].TrimEnd('/'), parts[1]);
            var client = http.CreateClient("wiki");

            var titles = await ListPagesAsync(client, baseUrl, ns, ct);
            foreach (var extra in parts.Skip(2))
            {
                var kv = extra.Split(':', 2);
                if (kv.Length != 2) continue;
                titles.AddRange((await ListPagesAsync(client, baseUrl, kv[0], ct))
                    .Where(t => t.Contains(kv[1], StringComparison.OrdinalIgnoreCase) && !t.Contains("Test", StringComparison.OrdinalIgnoreCase)));
            }
            titles = titles.Distinct().ToList();
            var entries = new List<WikiEntry>();
            var n = 0;
            foreach (var title in titles)
            {
                n++;
                Progress = $"Page {n} of {titles.Count}: {Short(title)}"; Changed?.Invoke();
                try { entries.AddRange(await ReadPageAsync(client, baseUrl, title, gameId, ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning("Wiki page {Title}: {Msg}", title, ex.Message); }
                await Task.Delay(300, ct);   // be kind to a small community wiki
            }
            if (entries.Count == 0) return 0;   // keep the old library rather than an empty one
            // "X Events" and "X Events Table" show the same table: keep each thing once.
            entries = entries.GroupBy(e => (e.Kind.ToLowerInvariant(), e.Title.ToLowerInvariant(), e.Text)).Select(g => g.First()).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.WikiEntries.Where(e => e.GameId == gameId).ExecuteDeleteAsync(ct);
            db.WikiEntries.AddRange(entries);
            game.WikiUpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            lock (_cacheLock) _cache.Remove(gameId);
            log.LogInformation("Wiki library for {Game}: {Count} entries from {Pages} pages", game.Slug, entries.Count, titles.Count);
            return entries.Count;
        }
        finally
        {
            Importing = false; Progress = ""; Changed?.Invoke();
            _importLock.Release();
        }
    }

    private static async Task<List<string>> ListPagesAsync(HttpClient client, string baseUrl, string ns, CancellationToken ct)
    {
        var titles = new List<string>();
        string? from = null;
        do
        {
            var url = $"{baseUrl}/api.php?action=query&list=allpages&apnamespace={ns}&aplimit=500&format=json" +
                      (from is null ? "" : "&apcontinue=" + Uri.EscapeDataString(from));
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            foreach (var p in doc.RootElement.GetProperty("query").GetProperty("allpages").EnumerateArray())
                titles.Add(p.GetProperty("title").GetString() ?? "");
            from = doc.RootElement.TryGetProperty("continue", out var c) && c.TryGetProperty("apcontinue", out var a) ? a.GetString() : null;
        } while (from is not null);
        return titles.Where(t => t.Length > 0).ToList();
    }

    private static async Task<List<WikiEntry>> ReadPageAsync(HttpClient client, string baseUrl, string title, int gameId, CancellationToken ct)
    {
        var url = $"{baseUrl}/api.php?action=parse&page={Uri.EscapeDataString(title)}&prop=text&disableeditsection=1&format=json";
        using var json = JsonDocument.Parse(await client.GetStringAsync(url, ct));
        if (!json.RootElement.TryGetProperty("parse", out var parse)) return [];
        var html = parse.GetProperty("text").GetProperty("*").GetString() ?? "";
        var doc = new HtmlParser().ParseDocument(html);
        var pageName = Short(title);
        var pageUrl = $"{baseUrl}/index.php?title={Uri.EscapeDataString(title.Replace(' ', '_'))}";
        var kind = System.Text.RegularExpressions.Regex.Replace(pageName, @"^GC4[ :]|\s*GC4$|\s*Table$", "").Trim();
        if (kind.Length == 0) kind = pageName;
        pageName = System.Text.RegularExpressions.Regex.Replace(pageName, @"^GC4[ :]|\s*GC4$", "").Trim();
        var result = new List<WikiEntry>();

        // Each row of a wiki table is one thing to look up.
        foreach (var table in doc.QuerySelectorAll("table.wikitable").ToList())
        {
            var rows = table.QuerySelectorAll("tr").ToList();
            var headers = rows.FirstOrDefault()?.QuerySelectorAll("th").Select(h => Clean(h.TextContent)).ToList() ?? [];
            foreach (var row in rows.Skip(1))
            {
                var cells = row.Children.Where(c => c.LocalName is "td" or "th").ToList();
                if (cells.Count == 0) continue;
                var name = Clean((cells[0].QuerySelector("b, strong, a:not(.mw-file-description)") ?? cells[0]).TextContent);
                if (name.Length == 0 || name.Length > 80) continue;
                var text = new StringBuilder();
                for (var i = 1; i < cells.Count; i++)
                {
                    var v = CellText(cells[i]);
                    if (v.Length == 0) continue;
                    var h = i < headers.Count ? headers[i] : "";
                    text.Append(h.Length > 0 ? $"**{h}:** " : "").AppendLine(v).AppendLine();
                }
                var img = cells[0].QuerySelector("img")?.GetAttribute("src") ?? "";
                result.Add(new WikiEntry
                {
                    GameId = gameId, Title = name, Kind = kind, Text = text.ToString().Trim(),
                    Url = pageUrl + (row.Id is { Length: > 0 } id ? "#" + id : ""),
                    Icon = img.StartsWith("/") ? baseUrl + img : img,
                });
            }
            table.Remove();
        }

        // What is left of the page (intro, sections, lists) is one page entry.
        var body = new StringBuilder();
        foreach (var el in doc.Body?.QuerySelectorAll("h2, h3, h4, p, li") ?? Enumerable.Empty<IElement>())
        {
            if (el.Closest("table, .toc, .navbox") is not null) continue;
            var t = Clean(el.TextContent);
            if (t.Length == 0) continue;
            body.AppendLine(el.LocalName switch { "h2" or "h3" or "h4" => "## " + t, "li" => "- " + t, _ => t });
        }
        if (body.Length > 40)
            result.Add(new WikiEntry { GameId = gameId, Title = pageName, Kind = "Page", Text = body.ToString().Trim(), Url = pageUrl });
        return result;
    }

    // Keeps the line structure of a cell (paragraphs, list items) as "- " lines.
    private static string CellText(IElement cell)
    {
        var items = cell.QuerySelectorAll("li, p").ToList();
        if (items.Count == 0) return Clean(cell.TextContent);
        var lines = new List<string>();
        var rest = Clean(string.Concat(cell.ChildNodes.Where(n => n is not IElement e || e.LocalName is not ("ul" or "ol" or "p")).Select(n => n.TextContent)));
        if (rest.Length > 0) lines.Add(rest);
        lines.AddRange(items.Where(i => i.QuerySelector("li, p") is null).Select(i => Clean(i.TextContent)).Where(t => t.Length > 0).Select(t => "- " + t));
        return string.Join("\n", lines);
    }

    private static string Clean(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.Replace(' ', ' '), @"\s+", " ").Trim();

    private static string Short(string title) => title.Contains(':') ? title[(title.IndexOf(':') + 1)..] : title;

    // ---------------------------------------------------------------- search

    public async Task<List<WikiEntry>> EntriesAsync(int gameId)
    {
        lock (_cacheLock) if (_cache.TryGetValue(gameId, out var hit)) return hit;
        await using var db = await dbFactory.CreateDbContextAsync();
        var list = await db.WikiEntries.AsNoTracking().Where(e => e.GameId == gameId).OrderBy(e => e.Title).ToListAsync();
        lock (_cacheLock) _cache[gameId] = list;
        return list;
    }

    public sealed record Hit(WikiEntry Entry, double Score, bool Close);

    /// <summary>Best matches first. "Close" = found only by spelling similarity.</summary>
    public async Task<List<Hit>> SearchAsync(int gameId, string query, string? kind = null, int max = 40)
    {
        var all = await EntriesAsync(gameId);
        if (kind is not null) all = all.Where(e => e.Kind == kind).ToList();
        var q = Norm(query);
        if (q.Length == 0) return all.Take(max).Select(e => new Hit(e, 0, false)).ToList();
        var qTokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var hits = new List<Hit>();
        foreach (var e in all)
        {
            var t = Norm(e.Title);
            double s = 0; var close = false;
            if (t == q) s = 100;
            else if (t.StartsWith(q)) s = 85;
            else if (t.Contains(q)) s = 70;
            else if (qTokens.All(t.Contains)) s = 60;
            else
            {
                // Spelling: best similarity of each query word against the title's words.
                var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var sim = qTokens.Average(qt => words.Length == 0 ? 0 : words.Max(w => Similarity(qt, w)));
                var whole = Similarity(q.Replace(" ", ""), t.Replace(" ", ""));
                var best = Math.Max(sim, whole);
                if (best >= 0.6) { s = 40 * best; close = true; }
                else
                {
                    var body = Norm(e.Text);
                    if (body.Contains(q)) s = 25;
                    else if (qTokens.Length > 1 && qTokens.All(body.Contains)) s = 15;
                }
            }
            if (s > 0) hits.Add(new Hit(e, s + (e.Kind == "Page" ? 0 : 1), close));
        }
        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Entry.Title.Length).Take(max).ToList();
    }

    private static string Norm(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    // 1 - Levenshtein distance / longer length.
    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var d = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) d[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var prev = d[0]; d[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var tmp = d[j];
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = tmp;
            }
        }
        return 1.0 - (double)d[b.Length] / Math.Max(a.Length, b.Length);
    }
}
