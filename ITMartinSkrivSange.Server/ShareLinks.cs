using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ITMartinSkrivSange.Application;

namespace ITMartinSkrivSange.Server;

// "Del" on a take (user 2026-10-25: "a private link plz" - a cover is fine for family and friends, not for the open web).
// The link holds the take's path and a signature made from the access code, so only links made here work, nothing is
// saved, and no PIN is asked. It plays that one take and nothing else. A new access code = all old links stop.
public sealed class ShareLinks(string secret)
{
    public string Key(string relativePath) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes("lyt:" + secret), Encoding.UTF8.GetBytes(relativePath)))[..22]
            .Replace('+', '-').Replace('/', '_');

    public bool Valid(string? relativePath, string? key) =>
        !string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(key)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Key(relativePath)), Encoding.ASCII.GetBytes(key));

    public string Url(string baseUri, string relativePath) =>
        $"{baseUri.TrimEnd('/')}/lyt?f={Uri.EscapeDataString(relativePath)}&k={Key(relativePath)}";
}

public static class ShareEndpoints
{
    public static void MapShareLinks(this WebApplication app)
    {
        app.MapGet("/lyt/lyd", (string? f, string? k, ShareLinks links, IStudioLibrary library) =>
            links.Valid(f, k) && IsTake(f!) && library.Resolve(f!) is { } full
                ? Results.File(full, "audio/mpeg", enableRangeProcessing: true)
                : Results.NotFound());

        app.MapGet("/lyt", async (string? f, string? k, ShareLinks links, IStudioLibrary library, SongWriting songs) =>
        {
            if (!links.Valid(f, k) || !IsTake(f!) || library.Resolve(f!) is null)
                return Results.Content(Page("Linket virker ikke", "<p>Optagelsen findes ikke længere, eller linket er skrevet forkert.</p>"), "text/html; charset=utf-8");
            var title = SongId(f!) is { } id && await songs.GetAsync(id) is { } song ? song.Title : "En sang";
            var src = $"/lyt/lyd?f={Uri.EscapeDataString(f!)}&k={k}";
            var body = $"""
                <p>Spillet og optaget af Martin.</p>
                <audio controls preload="metadata" src="{src}" style="width:100%;margin:1rem 0"></audio>
                <p class="k-small k-muted">Tryk på ▶ for at lytte. Linket er kun til familie og venner – del det ikke offentligt.</p>
                """;
            return Results.Content(Page(title, body), "text/html; charset=utf-8");
        });
    }

    // Only takes (mp3 under recordings/), never sketches or the music library.
    private static bool IsTake(string path) =>
        Path.GetFileName(path).StartsWith("take-", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    // ".../sang-7915fbcfd9e94c168e96f3da9faadcaf/take-intro-....mp3" -> the song's id.
    private static Guid? SongId(string path) =>
        Regex.Match(path, @"sang-([0-9a-f]{32})", RegexOptions.IgnoreCase) is { Success: true } m && Guid.TryParseExact(m.Groups[1].Value, "N", out var id) ? id : null;

    private static string Page(string title, string body)
    {
        var t = WebUtility.HtmlEncode(title);
        return $"""
            <!doctype html>
            <html lang="da">
            <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <meta name="robots" content="noindex, nofollow" />
            <title>{t} - Kolibri SkrivSange</title>
            <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css" />
            </head>
            <body>
            <main style="max-width:640px;margin:0 auto;padding:16px">
            <section class="k-card">
            <h1 class="k-card-title">{t}</h1>
            {body}
            </section>
            </main>
            </body>
            </html>
            """;
    }
}
