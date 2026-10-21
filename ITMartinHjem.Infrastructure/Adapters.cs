using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using WebPush;

namespace ITMartinHjem.Infrastructure;

// The settings the adapters need (Hjem__* in magic.env), read once in AddHjemInfrastructure.
public sealed class HjemInfraOptions
{
    public string DataDir { get; set; } = "/app/data";
    public string? ClaudeApiKey { get; set; }
    public string SmtpHost { get; set; } = "send.one.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "info@itmartin.dk";
    public string? SmtpPassword { get; set; }
    public string SmtpReplyTo { get; set; } = "ITMartin@Mensa.dk";
    public string KigMedStatus { get; set; } = "http://10.0.0.200:9144/api/status";
    public string ElPriserSnapshot { get; set; } = "http://10.0.0.126:8120/api/snapshot?area=DK1&allIn=true";
    /// <summary>Kunder, the Kundekartotek for all apps (Kunder__Url / Kunder__ApiKey, same names in every app).</summary>
    public string? KunderUrl { get; set; }
    public string? KunderApiKey { get; set; }
}

// Files live under <DataDir>/media. Photos are turned upright from EXIF once, at upload (the old Olympus
// sideways-thumbnail trouble must never happen here), and kept in two sizes: a 2000 px "full" and an 800 px thumbnail.
// Everything else is stored as-is.
internal sealed class MediaStore : IMediaStore
{
    private readonly string _dir;

    public MediaStore(HjemInfraOptions o)
    {
        _dir = Path.Combine(o.DataDir, "media");
        Directory.CreateDirectory(_dir);
    }

    private string PathFor(string stored) => Path.Combine(_dir, Path.GetFileName(stored));

    public string? Find(string storedName) => storedName.Length > 0 && File.Exists(PathFor(storedName)) ? PathFor(storedName) : null;

    public async Task<SavedMedia> SaveAsync(Stream input, string fileName, string contentType, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N")[..16];
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var kind = Media.KindOf(contentType, ext);

        if (kind == MediaKind.Billede && ext is not ".svg" and not ".gif")
        {
            // ImageSharp needs a seekable stream; browser upload streams are not.
            using var buf = new MemoryStream();
            await input.CopyToAsync(buf, ct);
            buf.Position = 0;
            using var img = await Image.LoadAsync(buf, ct);
            var taken = TakenAt(img);
            img.Mutate(x => x.AutoOrient());
            img.Metadata.ExifProfile = null;
            var enc = new JpegEncoder { Quality = 85 };
            using (var full = img.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(2000, 2000) })))
                await full.SaveAsJpegAsync(PathFor(id + ".jpg"), enc, ct);
            using (var thumb = img.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(800, 800) })))
                await thumb.SaveAsJpegAsync(PathFor(id + "_t.jpg"), enc, ct);
            return new(new Media { Kind = kind, StoredName = id + ".jpg", ThumbName = id + "_t.jpg", ContentType = "image/jpeg", FileName = fileName }, taken);
        }

        await using (var fs = File.Create(PathFor(id + ext)))
            await input.CopyToAsync(fs, ct);
        return new(new Media { Kind = kind, StoredName = id + ext, ContentType = contentType.Length > 0 ? contentType : "application/octet-stream", FileName = fileName }, null);
    }

    public void Delete(string storedName, string? thumbName)
    {
        foreach (var n in new[] { storedName, thumbName })
            if (!string.IsNullOrEmpty(n) && File.Exists(PathFor(n))) File.Delete(PathFor(n));
    }

    private static DateTime? TakenAt(Image img)
    {
        var exif = img.Metadata.ExifProfile;
        if (exif is null || !exif.TryGetValue(ExifTag.DateTimeOriginal, out var v) || v?.Value is not { } s) return null;
        return DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }
}

// Web Push (same VAPID pattern as MinElpris). Keys are made once and kept on the data volume - new keys would orphan
// every subscribed phone.
internal sealed class WebPushSender : IPushSender
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<WebPushSender> _log;

    public string PublicKey => _vapid.PublicKey;

    public WebPushSender(HjemInfraOptions o, ILogger<WebPushSender> log)
    {
        _log = log;
        Directory.CreateDirectory(o.DataDir);
        var path = Path.Combine(o.DataDir, "vapid.json");
        if (!File.Exists(path))
        {
            var k = VapidHelper.GenerateVapidKeys();
            File.WriteAllText(path, JsonSerializer.Serialize(new Keys(k.PublicKey, k.PrivateKey)));
        }
        var keys = JsonSerializer.Deserialize<Keys>(File.ReadAllText(path))!;
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", keys.PublicKey, keys.PrivateKey);
    }

    private sealed record Keys(string PublicKey, string PrivateKey);

    public async Task<PushResult> SendAsync(PushTarget to, string title, string body, string url, CancellationToken ct = default)
    {
        try
        {
            await _client.SendNotificationAsync(new PushSubscription(to.Endpoint, to.P256dh, to.Auth),
                JsonSerializer.Serialize(new { title, body, url }), _vapid, ct);
            _log.LogInformation("Push accepted by {Host}: {Title}", new Uri(to.Endpoint).Host, title);
            return PushResult.Sent;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound) { return PushResult.Gone; }
        catch (WebPushException ex)
        {
            _log.LogWarning("Push refused by {Host}: {Status} {Body}", new Uri(to.Endpoint).Host, ex.StatusCode,
                ex.HttpResponseMessage is { } r ? await r.Content.ReadAsStringAsync(ct) : "");
            return PushResult.Failed;
        }
        catch (Exception ex) { _log.LogWarning(ex, "Push failed"); return PushResult.Failed; }
    }
}

// SMTP = the one.com mailbox info@itmartin.dk (Hjem__Smtp__Password in magic.env, set by the owner).
// Without a password nothing is sent - the chat and the browser notification still work.
internal sealed class SmtpMailSender(HjemInfraOptions o, ILogger<SmtpMailSender> log) : IMailSender
{
    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(o.SmtpPassword)) { log.LogWarning("Mail not sent: Hjem__Smtp__Password is not set"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(o.SmtpUser, "Martin Hvidberg · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
            };
            msg.ReplyToList.Add(new MailAddress(o.SmtpReplyTo, "Martin Hvidberg"));
            using var smtp = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = true, Credentials = new NetworkCredential(o.SmtpUser, o.SmtpPassword) };
            await smtp.SendMailAsync(msg, ct);
            log.LogInformation("Mail sent: {Subject}", subject);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail failed");
            return false;
        }
    }
}

// Checks Martin's own text before it goes on the site (Svar): spelling, bullet form for task lists, and anything a
// visitor could misread. One Sonnet call per save the owner presses - never in a loop - with a hard daily cap in code.
internal sealed class ClaudeTextChecker(HjemInfraOptions o, ILogger<ClaudeTextChecker> logger) : ITextChecker
{
    private const int MaxPerDay = 200;
    private readonly AnthropicClient? _client = string.IsNullOrWhiteSpace(o.ClaudeApiKey) ? null : new AnthropicClient { ApiKey = o.ClaudeApiKey };
    private readonly object _lock = new();
    private DateOnly _day;
    private int _used;

    public async Task<TextReview?> CheckAsync(string text, TextKind kind, CancellationToken ct = default)
    {
        text = text.Trim();
        if (_client is null || text.Length == 0 || !Take()) return null;
        var form = kind switch
        {
            TextKind.Liste => "Hele teksten er en opgaveliste, så ALT skal i punktform.",
            TextKind.Linje => "Teksten er en kort tekst (en linje til et billede, en status, en overskrift eller en kort beskrivelse). Er der ingen opgaver, så hold den kort som almindelig tekst.",
            TextKind.Chat => "Teksten er Martins svar til en besøgende i chatten på hjemmesiden. Hold tonen venlig og personlig, som Martin skrev den.",
            _ => "Teksten er et opslag eller en side på hjemmesiden. Bevar afsnit og opbygning, og lad markdown, billedlinjer, links og emojis stå uændret; brug også punktform, hvor der er en opremsning.",
        };
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model.ClaudeSonnet4_6,   // Haiku missed Danish names and did not split tasks into bullets (tested 2026-09-29)
                MaxTokens = kind == TextKind.Opslag ? 16000 : 1500,   // whole pages come back in full (Bliv pilot is ~24,000 characters)
                System = "Du er korrekturlæser for Martin, der lægger tekst på sin egen hjemmeside (itmartin.dk). " +
                         "Ret stavefejl, slåfejl og grammatik på dansk. Martins egne navne staves sådan: ITMartin, ITKolibri, MinElpris, ElPriser, Svar, Bogshoppen, Forløbet, Polstrer, R6, Mensa. " +
                         "OPGAVER SKAL ALTID STÅ I PUNKTFORM: når teksten nævner opgaver eller gøremål (noget Martin skal, vil, regner med eller har lavet), " +
                         "skriv dem som punkter – én opgave pr. linje, hver linje starter med \"* \", stort begyndelsesbogstav, intet punktum til sidst. " +
                         "Del sætninger med flere opgaver op (fx \"skrive på X og ringe til Y\" bliver \"* Skrive på X\" og \"* Ringe til Y\"). " +
                         "Almindelig tekst omkring opgaverne står som almindelig tekst over eller under punkterne. Tilføj ingen nye opgaver. " + form + " " +
                         "Bevar Martins egne ord, tone og humor (også emojis) – omskriv ikke mere end nødvendigt, og opfind intet. " +
                         "Se efter ting, der kan misforstås af en fremmed læser: tvetydige formuleringer, ironi der kan læses bogstaveligt, " +
                         "noget der kan virke stødende, løfter om priser eller tider, og personlige oplysninger om andre (navne på børn, adresser, helbred). " +
                         "Skriv hver sådan ting som en kort note på dansk, der siger hvad og hvorfor. Stavefejl skal IKKE i noterne – de er rettet i teksten. Ingen noter, hvis der ikke er noget. " +
                         "Svar KUN med JSON: {\"tekst\": \"den rettede tekst\", \"noter\": [\"…\"]}. Linjeskift i teksten som \\n.",
                Messages = [new() { Role = Role.User, Content = text }]
            }, cancellationToken: ct);

            var raw = "";
            foreach (var block in response.Content)
                if (block.TryPickText(out var tb)) raw += tb.Text;
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var suggested = (doc.RootElement.TryGetProperty("tekst", out var t) ? t.GetString() : null)?.Trim() ?? text;
            var notes = doc.RootElement.TryGetProperty("noter", out var n) && n.ValueKind == JsonValueKind.Array
                ? n.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                : [];
            if (suggested.Length == 0) suggested = text;
            // A cut-off or shortened answer must never replace the owner's text.
            if (suggested.Length < text.Length * 0.8) return notes.Count > 0 ? new TextReview(text, notes, false) : null;
            return new TextReview(suggested, notes, !Same(suggested, text));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Text check failed - saving as written");
            return null;
        }
    }

    // Whitespace-only differences are not a change worth asking about.
    private static bool Same(string a, string b) =>
        string.Join('\n', a.Split('\n').Select(l => l.Trim())) == string.Join('\n', b.Split('\n').Select(l => l.Trim()));

    private bool Take()
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= MaxPerDay) { logger.LogWarning("Text check budget spent ({Max}/day)", MaxPerDay); return false; }
            _used++;
            return true;
        }
    }
}

// The salt is made once and kept on the data volume (<DataDir>/ip-salt), so the hashes stay the same across deploys.
internal sealed class SaltedHasher : IVisitorHasher
{
    private readonly byte[] _salt;

    public SaltedHasher(HjemInfraOptions o)
    {
        Directory.CreateDirectory(o.DataDir);
        var saltFile = Path.Combine(o.DataDir, "ip-salt");
        if (!File.Exists(saltFile)) File.WriteAllBytes(saltFile, RandomNumberGenerator.GetBytes(32));
        _salt = File.ReadAllBytes(saltFile);
    }

    public string Hash(string value) => Convert.ToHexString(SHA256.HashData([.. _salt, .. Encoding.UTF8.GetBytes(value)]))[..16];
}

// Is each app on "Mine apps" running? Checked every 2 minutes from here, so a visitor never waits for it and never
// sees a Cloudflare 502. 5xx or no answer = sleeping; anything else = up (Upload answers 404 on its bare front page).
internal sealed class AppStatusChecker(IAppLinkRepository apps, IHttpClientFactory http, ILogger<AppStatusChecker> log) : BackgroundService, IAppStatus
{
    public const string Client = "status";
    private readonly ConcurrentDictionary<string, bool> _up = new();

    public bool? IsUp(string url) => _up.TryGetValue(url, out var up) ? up : null;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        do
        {
            try
            {
                var urls = (await apps.AllAsync(ct)).Where(a => a.Url != "").Select(a => a.Url).Distinct().ToList();
                var client = http.CreateClient(Client);
                await Parallel.ForEachAsync(urls, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (url, c) =>
                {
                    try
                    {
                        using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, c);
                        _up[url] = (int)res.StatusCode < 500;
                    }
                    catch { _up[url] = false; }
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "App status check failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

// Kig med (kigmed-web, Hjem:KigMedStatus) and ElPriser (/api/snapshot, Hjem:ElPriserSnapshot) over the LAN.
internal sealed class NeighbourApps(IHttpClientFactory http, HjemInfraOptions o) : INeighbourApps
{
    public async Task<LiveStatus?> KigMedAsync(CancellationToken ct = default)
    {
        using var c = http.CreateClient();
        c.Timeout = TimeSpan.FromSeconds(2);
        return await c.GetFromJsonAsync<LiveStatus>(o.KigMedStatus, ct);
    }

    public async Task<IReadOnlyList<ApplianceSnapshot>?> ElPriserAppliancesAsync(CancellationToken ct = default)
    {
        using var c = http.CreateClient();
        c.Timeout = TimeSpan.FromSeconds(3);
        var s = await c.GetFromJsonAsync<Snap>(o.ElPriserSnapshot, ct);
        return s?.Appliances?.Where(a => a.Appliance is not null)
            .Select(a => new ApplianceSnapshot(a.Appliance!.Name, a.Appliance.KwhPerRun, a.Appliance.DurationHours, W(a.Now), W(a.CheapestToday), W(a.CheapestTomorrow)))
            .ToList();
    }

    private static ApplianceWindow? W(Window? w) => w is null ? null : new(w.Start, w.CostKr);

    private sealed record Snap(List<ApplianceCost>? Appliances);
    private sealed record ApplianceCost(ApplianceInfo? Appliance, Window? Now, Window? CheapestToday, Window? CheapestTomorrow);
    private sealed record ApplianceInfo(string Name, decimal KwhPerRun, decimal DurationHours);
    private sealed record Window(DateTime Start, DateTime End, decimal CostKr);
}
