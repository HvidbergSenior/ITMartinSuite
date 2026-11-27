using System.Text.Json;
using System.Threading.Channels;
using ITMartin.Shared.UI.Kolibri;
using ITMartinMode.Application;
using ITMartinMode.Domain;
using ITMartinMode.Infrastructure;

// Møde (Kolibri Kommunikere) - 2026-11-27, user: "a simple online meeting room between the four people ...
// talk with each other and make the Acceleratorkursus game" (De 6 HV - Stjernespidserne).
// Video: each person WHIP-publishes their own path on our MediaMTX (kigmed-media), the page WHEP-plays the others.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Møde";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Et lille, lukket mødelokale på nettet – kun for dem med linket.";
    k.Version = "2026.11.27.1";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Et lille mødelokale på nettet for en fast gruppe – kun dem med linket kan komme ind.",
        "I ser og hører hinanden, og I kan udfylde De 6 HV-stjernen sammen. Intet bliver optaget.",
    ];
    k.HowTo =
    [
        "Åbn linket i Chrome eller Safari på en computer med kamera og mikrofon.",
        "Skriv dit navn og tryk Gå ind. Sig Tillad til kamera og mikrofon.",
        "Skriv sedler i stjernens spidser – alle ser dem med det samme.",
        "Tryk Forlad mødet, når I er færdige.",
    ];
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Mode:DataDir"] ?? "/app/data")),
    ]);
});

// Rooms come from magic.env: Mode__Rooms__0__Slug=gruppe1, Mode__Rooms__0__Title=..., Mode__Rooms__0__Key=...
var rooms = builder.Configuration.GetSection("Mode:Rooms").GetChildren()
    .Select(s => new RoomSetup(s["Slug"] ?? "", s["Title"] ?? "Møde", s["Key"] ?? "")).ToList();
var dataDir = builder.Configuration["Mode:DataDir"] ?? "/app/data";
builder.Services.AddSingleton(new MeetingService(rooms, new JsonBoardStore(dataDir)));
builder.Services.AddSingleton<LiveFeed>();
builder.Services.AddHostedService<Sweeper>();
builder.Services.AddHttpClient("media", c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var app = builder.Build();
app.Services.GetRequiredService<MeetingService>().Changed += slug => app.Services.GetRequiredService<LiveFeed>().Poke(slug);
app.MapKolibri();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html")) ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
IResult Fejl(string m) => Results.BadRequest(new { fejl = m });

// The room's page: /gruppe1?k=... (same static page; the script reads slug + key from the address)
app.MapGet("/{slug:regex(^[a-z0-9-]{{2,30}}$)}", (string slug, MeetingService m, IWebHostEnvironment env) =>
    m.Exists(slug)
        ? Results.File(Path.Combine(env.WebRootPath, "index.html"), "text/html; charset=utf-8")
        : Results.NotFound());

var api = app.MapGroup("/api/{slug}");

api.MapPost("/ind", (string slug, JoinIn b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
    try
    {
        var p = m.Join(r, b.Navn);
        return Results.Json(new { id = p.Id, token = p.Token, path = p.MediaPath(r.Slug) }, web);
    }
    catch (DomainException e) { return Fejl(e.Message); }
});

api.MapPost("/ud", (string slug, Who b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is { } r) m.Leave(r, b.Id, b.Token);
    return Results.Ok();
});

api.MapPost("/hej", (string slug, Who b, MeetingService m) =>
    m.Door(slug, b.K) is { } r && m.Alive(r, b.Id, b.Token) ? Results.Ok() : Results.StatusCode(410));

api.MapPost("/seddel", (string slug, NoteIn b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
    if (!Enum.TryParse<StarPoint>(b.Point, out var point)) return Fejl("Ukendt spids.");
    try { m.AddNote(r, b.Id, b.Token, point, b.Text); return Results.Ok(); }
    catch (DomainException e) { return Fejl(e.Message); }
});

api.MapPost("/fjern", (string slug, RemoveIn b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
    try { m.RemoveNote(r, b.Id, b.Token, b.NoteId ?? ""); return Results.Ok(); }
    catch (DomainException e) { return Fejl(e.Message); }
});

api.MapPost("/ide", (string slug, IdeaIn b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
    try { m.SetIdea(r, b.Id, b.Token, b.Idea); return Results.Ok(); }
    catch (DomainException e) { return Fejl(e.Message); }
});

api.MapPost("/ryd", (string slug, Who b, MeetingService m) =>
{
    if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
    try { m.ClearBoard(r, b.Id, b.Token); return Results.Ok(); }
    catch (DomainException e) { return Fejl(e.Message); }
});

// The game: anyone in the room may start it, move on to the next turn, or stop it.
foreach (var (route, act) in new (string, Action<MeetingService, Room, string?, string?>)[]
{
    ("spil/start", (m, r, i, t) => m.StartGame(r, i, t)),
    ("spil/naeste", (m, r, i, t) => m.NextTurn(r, i, t)),
    ("spil/stop", (m, r, i, t) => m.StopGame(r, i, t)),
})
    api.MapPost("/" + route, (string slug, Who b, MeetingService m) =>
    {
        if (m.Door(slug, b.K) is not { } r) return Results.StatusCode(403);
        try { act(m, r, b.Id, b.Token); return Results.Ok(); }
        catch (DomainException e) { return Fejl(e.Message); }
    });

// Live: the whole room view on every change (a handful of people and notes - small).
api.MapGet("/live", async (HttpContext c, string slug, string? k, MeetingService m, LiveFeed live) =>
{
    if (m.Door(slug, k) is not { } r) { c.Response.StatusCode = 403; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join(r.Slug);
    try
    {
        async Task Send() =>
            await c.Response.WriteAsync("data: " + JsonSerializer.Serialize(m.View(r), web) + "\n\n", c.RequestAborted);
        await Send();
        await c.Response.Body.FlushAsync(c.RequestAborted);
        while (!c.RequestAborted.IsCancellationRequested)
        {
            // a comment every 20 s keeps Cloudflare from closing a quiet line
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
            wait.CancelAfter(TimeSpan.FromSeconds(20));
            try { await reader.ReadAsync(wait.Token); while (reader.TryRead(out _)) { } await Send(); }
            catch (OperationCanceledException) when (!c.RequestAborted.IsCancellationRequested)
            { await c.Response.WriteAsync(": ping\n\n", c.RequestAborted); }
            await c.Response.Body.FlushAsync(c.RequestAborted);
        }
    }
    catch (OperationCanceledException) { }
    finally { live.Leave(r.Slug, id); }
});

// ── Video: MediaMTX behind this app (same as Kig med). Publish = only your own path, with your token.
var mediaWebRtc = app.Configuration["Mode:MediaWebRtc"] ?? "http://kigmed-media:8889";
var publishUser = app.Configuration["Mode:PublishUser"] ?? "mode";
var publishPass = app.Configuration["Mode:PublishPass"] ?? "";

app.Map("/media/{slug}/{**path}", async (HttpContext ctx, IHttpClientFactory http, MeetingService m, string slug, string path) =>
{
    // path = "mode-<slug>-<id>/whip" (publish) or ".../whep" (play), plus WHIP/WHEP's follow-up "/<session>"
    var parts = path.Split('/', 3);
    if (parts.Length < 2 || !parts[0].StartsWith($"mode-{slug}-")) { ctx.Response.StatusCode = 404; return; }
    if (m.Door(slug, ctx.Request.Query["k"]) is not { } r) { ctx.Response.StatusCode = 403; return; }
    var publish = parts[1] == "whip";
    if (publish && !m.CanPublish(r, ctx.Request.Query["p"], ctx.Request.Query["t"], parts[0])) { ctx.Response.StatusCode = 401; return; }

    var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), $"{mediaWebRtc}/{path}");
    if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        req.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType is { } ct) req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
    }
    foreach (var h in new[] { "If-Match", "Accept" })
        if (ctx.Request.Headers.TryGetValue(h, out var v)) req.Headers.TryAddWithoutValidation(h, v.ToArray());
    if (publish && publishPass.Length > 0)
        req.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{publishUser}:{publishPass}")));
    try
    {
        using var res = await http.CreateClient("media").SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        ctx.Response.StatusCode = (int)res.StatusCode;
        foreach (var (key, val) in res.Headers.Concat(res.Content.Headers))
        {
            if (key is "Transfer-Encoding" or "Connection" or "Server" or "Access-Control-Allow-Origin" or "Access-Control-Allow-Credentials") continue;
            var values = val.ToArray();
            // the session's own address stays on this host and keeps the door key (PATCH/DELETE come back through here)
            if (key == "Location") values = values.Select(l => l.StartsWith('/') ? $"/media/{slug}{l}{ctx.Request.QueryString}" : l).ToArray();
            ctx.Response.Headers[key] = values;
        }
        ctx.Response.Headers.CacheControl = "no-store";
        await res.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
    catch (HttpRequestException) { ctx.Response.StatusCode = 502; }
}).DisableAntiforgery();

app.Run();

record JoinIn(string? K, string? Navn);
record Who(string? K, string? Id, string? Token);
record NoteIn(string? K, string? Id, string? Token, string? Point, string? Text);
record RemoveIn(string? K, string? Id, string? Token, string? NoteId);
record IdeaIn(string? K, string? Id, string? Token, string? Idea);

/// <summary>Open live pages per room; a poke wakes them all to send the fresh view.</summary>
sealed class LiveFeed
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<Guid, Channel<bool>>> _rooms = new();

    public (Guid, ChannelReader<bool>) Join(string slug)
    {
        var ch = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        var id = Guid.NewGuid();
        lock (_gate)
        {
            if (!_rooms.TryGetValue(slug, out var d)) _rooms[slug] = d = new();
            d[id] = ch;
        }
        return (id, ch.Reader);
    }

    public void Leave(string slug, Guid id) { lock (_gate) if (_rooms.TryGetValue(slug, out var d)) d.Remove(id); }

    public void Poke(string slug)
    {
        lock (_gate) if (_rooms.TryGetValue(slug, out var d)) foreach (var ch in d.Values) ch.Writer.TryWrite(true);
    }
}

/// <summary>Every 10 s: people whose page went quiet have left.</summary>
sealed class Sweeper(MeetingService m) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(10), stop); } catch (OperationCanceledException) { return; }
            m.SweepAll();
        }
    }
}
