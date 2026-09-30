using ITMartin.Shared.UI.Kolibri;
using ITMartinKigMed.Server;
using ITMartinKigMed.Server.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Kig med";
    k.KolibriName = "ITKolibri · Reagere Live";
    k.Family = "reagere";
    k.Tagline = "Se Martin arbejde live – og vær med: stil spørgsmål, stem og send idéer.";
    k.About =
    [
        "Kig med viser Martin live på video, når han koder, spiller guitar eller viser en app frem.",
        "Du kan stille spørgsmål, stemme når han spørger, sende reaktioner og sende en idé eller et billede ind – direkte fra telefonen, uden app og uden konto.",
    ];
    k.HowTo =
    [
        "Åbn siden, når Martin er live – videoen starter af sig selv. Tryk 🔊 for lyd.",
        "Skriv dit navn én gang. Så kan du stille spørgsmål i feltet under videoen.",
        "Når Martin spørger om noget, dukker afstemningen op – tryk på dit svar.",
        "Tryk på en emoji for at reagere, eller send en idé/et billede med 💡.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);
builder.Services.AddSingleton<LiveHub>();
builder.Services.AddHttpClient("media", c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// ── The studio (Martin only) is behind one PIN; watching is open to everyone.
var pin = app.Configuration["KigMed:Pin"] ?? "kigmed2026";
bool IsOwner(HttpContext ctx) => ctx.Request.Cookies.TryGetValue("kigmed_auth", out var v) && v == pin;
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/studio", StringComparison.OrdinalIgnoreCase) && !IsOwner(ctx))
    {
        ctx.Response.Redirect("/login");
        return;
    }
    await next();
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin_) =>
{
    if (pin_ != pin) return Results.Redirect("/login?error=1");
    ctx.Response.Cookies.Append("kigmed_auth", pin, new CookieOptions
    {
        HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365),
        Secure = ctx.Request.IsHttps || ctx.Request.Headers.ContainsKey("CF-Connecting-IP"),
    });
    return Results.Redirect("/studio");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("kigmed_auth");
    return Results.Redirect("/");
});

// Pictures viewers sent in. Names are random guids; a picture Martin has not shown is only for the studio.
app.MapGet("/pic/{name}", (string name, HttpContext ctx, LiveHub hub) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[0-9a-f]{32}\\.jpg$")) return Results.NotFound();
    var shown = hub.Read(s => s.Ideas.Any(i => i.Picture == name && i.Shown));
    if (!shown && !IsOwner(ctx)) return Results.NotFound();
    var file = Path.Combine(hub.PicturesDir, name);
    return File.Exists(file) ? Results.File(file, "image/jpeg") : Results.NotFound();
});

// ── Video: MediaMTX (container kigmed-media) behind this app, so one hostname serves everything.
// /media/... = WebRTC signalling (WHIP publish from the studio, WHEP play for viewers), /hls/... = the fallback.
// Publishing needs the studio cookie; the app adds MediaMTX's publish login itself.
var mediaWebRtc = app.Configuration["KigMed:MediaWebRtc"] ?? "http://kigmed-media:8889";
var mediaHls = app.Configuration["KigMed:MediaHls"] ?? "http://kigmed-media:8888";
var publishUser = app.Configuration["KigMed:PublishUser"] ?? "martin";
var publishPass = app.Configuration["KigMed:PublishPass"] ?? "";

async Task Proxy(HttpContext ctx, IHttpClientFactory http, string target, string prefix, bool publish)
{
    if (publish && !IsOwner(ctx)) { ctx.Response.StatusCode = 401; return; }
    var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target + ctx.Request.QueryString);
    if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        req.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType is { } ct) req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
    }
    foreach (var h in new[] { "If-Match", "Accept" })
        if (ctx.Request.Headers.TryGetValue(h, out var v)) req.Headers.TryAddWithoutValidation(h, v.ToArray());
    // MediaMTX's HLS sets its own cookies (cookieCheck, session) and redirects - send those back, never the studio login
    var mtxCookies = ctx.Request.Cookies.Where(c => c.Key != "kigmed_auth").Select(c => $"{c.Key}={c.Value}").ToList();
    if (mtxCookies.Count > 0) req.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", mtxCookies));
    if (publish && publishPass.Length > 0)
        req.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{publishUser}:{publishPass}")));
    try
    {
        using var res = await http.CreateClient("media").SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        ctx.Response.StatusCode = (int)res.StatusCode;
        foreach (var (k, v) in res.Headers.Concat(res.Content.Headers))
        {
            if (k is "Transfer-Encoding" or "Connection" or "Server" or "Access-Control-Allow-Origin" or "Access-Control-Allow-Credentials") continue;
            var values = v.ToArray();
            // WHIP/WHEP answer with the session's own address - keep it on this host
            if (k == "Location") values = values.Select(l => l.StartsWith('/') ? prefix + l : l).ToArray();
            ctx.Response.Headers[k] = values;
        }
        ctx.Response.Headers.CacheControl = "no-store";
        await res.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
    catch (HttpRequestException)
    {
        ctx.Response.StatusCode = 502;
    }
}

app.Map("/media/{**path}", (HttpContext ctx, IHttpClientFactory http, string path) =>
    // WHIP's follow-up calls (PATCH candidates, DELETE on stop) go to live/whip/<session> too
    Proxy(ctx, http, $"{mediaWebRtc}/{path}", "/media", path.StartsWith("live/whip", StringComparison.OrdinalIgnoreCase)))
    .DisableAntiforgery();
app.MapGet("/hls/{**path}", (HttpContext ctx, IHttpClientFactory http, string path) =>
    Proxy(ctx, http, $"{mediaHls}/{path}", "/hls", false));

app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
