using System.Net;

// A gallery can be closed at a given time (Galleries__N__ClosesAt, Danish local time, e.g. 2026-09-29T23:59).
// Before that moment the pages show a warning banner (/api/closing + closing.js); after it every way into
// the gallery - the page, ?g=, the API and the picture files - answers "closed". Nothing is deleted:
// the pictures stay on the NAS, and access can be reopened (the business model: pay to keep watching).
public static class Closing
{
    public const string Contact = "ITMartin@Mensa.dk · 31 19 47 30";
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateTime NowDk() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Dk);

    // slug -> closing time, read from configuration on every call (cheap; follows config reloads).
    public static Dictionary<string, DateTime> Times(IConfiguration config) =>
        config.GetSection("Galleries").GetChildren()
            .Select(s => (Slug: s["Slug"] ?? "", At: DateTime.TryParse(s["ClosesAt"], out var at) ? at : (DateTime?)null))
            .Where(x => x.Slug.Length > 0 && x.At is not null)
            .ToDictionary(x => x.Slug.ToLowerInvariant(), x => x.At!.Value);

    // Which gallery a request is about: /slug, /libraryfiles/slug/…, /thumbs/slug/…, ?g=slug, ?gallery=slug.
    public static IEnumerable<string> SlugsOf(HttpRequest req)
    {
        var parts = (req.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0) yield return Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
        if (parts.Length > 1) yield return Uri.UnescapeDataString(parts[1]).ToLowerInvariant();
        foreach (var key in new[] { "g", "gallery", "slug" })
            if (req.Query[key].ToString() is { Length: > 0 } q) yield return q.ToLowerInvariant();
    }

    public static string Warning(DateTime at) =>
        $"⚠️ Dette galleri lukker {When(at)}. Billederne bliver ikke slettet – vil du beholde adgangen, kan du købe fortsat adgang hos ITMartin: {Contact}.";

    private static string When(DateTime at)
    {
        var da = new System.Globalization.CultureInfo("da-DK");
        var day = at.Date == NowDk().Date ? "i aften" : at.ToString("dddd 'den' d. MMMM", da);
        return $"{day} kl. {at:HH.mm}";
    }

    public static string ClosedPage(string name) => $$$"""
        <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Galleriet er lukket</title>
        <style>
          :root{--bg:#f6f4f1;--ink:#1f2328;--mut:#5d6470;--card:#fff;--acc:#6d28d9}
          @media(prefers-color-scheme:dark){:root{--bg:#121417;--ink:#eceff3;--mut:#a3aab5;--card:#1b1f24;--acc:#a78bfa}}
          body{margin:0;min-height:100vh;display:grid;place-items:center;background:var(--bg);color:var(--ink);font:17px/1.55 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;padding:16px}
          .card{max-width:520px;background:var(--card);border-radius:18px;padding:28px 24px;text-align:center;box-shadow:0 10px 40px rgba(0,0,0,.12)}
          .icon{font-size:3rem}h1{margin:.2em 0 .4em;font-size:1.6rem}p{color:var(--mut);margin:.5em 0}
          a{color:var(--acc);font-weight:700}
        </style></head><body><main class="card">
          <div class="icon">🔒</div>
          <h1>{{{WebUtility.HtmlEncode(name)}}} er lukket</h1>
          <p>Galleriet er ikke længere åbent. <b>Billederne er ikke slettet</b> – de ligger stadig trygt gemt.</p>
          <p>Vil du se dem igen? Du kan købe fortsat adgang. Skriv eller ring til ITMartin:</p>
          <p><a href="mailto:ITMartin@Mensa.dk">ITMartin@Mensa.dk</a> · <a href="tel:+4531194730">31 19 47 30</a></p>
        </main></body></html>
        """;
}
