using ITMartinSalgsveje.Application;
using ITMartinSalgsveje.Domain;
using ITMartinSalgsveje.Infrastructure;
using ITMartinSalgsveje.Server;
using ITMartinSalgsveje.Server.Services;
using ITMartin.Shared.UI.Kolibri;

// Salgsveje (copy of Club, 2026-11-04): the thin web layer - pages, login cookies, links, the reminder job. The rules are in .Domain,
// the use cases in .Application, SQLite/push/mail in .Infrastructure (DDD + Clean, 2026-10-11).

var builder = WebApplication.CreateBuilder(args);

// The shared Kolibri feel (header, footer, /om, /hjaelp) like ElPriser (2026-11-04).
builder.Services.AddKolibri(k =>
{
    k.Name = "Salgsveje";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Alle måder at sælge på - hvordan, hvem og hvornår.";
    k.About =
    [
        "Salgsveje samler alle de måder, vi kan sælge på: markeder, live-auktion, nettet, tillidsbod, en kasse ud til et plejehjem og meget mere.",
        "Hver salgsvej har et kort: hvordan det virker, hvem der gør hvad, hvornår - og hvad det gav. Så kan vi se, hvad der er værd at gøre igen.",
        "Bygget på Klub (opgaver pr. person og dag). Ingen reklamer.",
    ];
    k.HowTo =
    [
        "Åbn holdet med dit link, eller skriv dit navn og din PIN.",
        "Salgsveje: tryk på et kort for at se hvordan, hvem og hvornår. Tryk Ny salgsvej for at tilføje en idé.",
        "Hvem og hvornår: skriv en opgave, vælg en person og en dag. Opgaven kommer også på Opgaver.",
        "Når I har prøvet det: skriv hvad det gav i kr, og om I vil igen.",
    ];
    k.Version = "2026.11.04";
    k.ThemeColor = "#121917";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

var demoMode = builder.Configuration.GetValue<bool>("Club:SeedDemoData");
builder.Services.AddClubApplication(demoMode);
builder.Services.AddClubInfrastructure(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ClubBroadcastService>();
builder.Services.AddScoped<ClubAuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<MessageReminderJob>();

var app = builder.Build();

app.Services.UpgradeClubDatabase(demoMode);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.MapKolibri();
app.UseStaticFiles();
app.UseAntiforgery();

var photosRoot = ITMartinSalgsveje.Infrastructure.DependencyInjection.PhotosRoot(builder.Configuration);
Directory.CreateDirectory(photosRoot);

var liveMediaRoot = builder.Configuration["ClubSettings:LiveMediaRoot"] ?? "/app/data/livemedia";
Directory.CreateDirectory(liveMediaRoot);

app.MapPost("/api/push/subscribe", async (ClubPushRequest req, IPushNotifier push) =>
{
    await push.SubscribeAsync(new ClubPushSubscription
    {
        GroupId    = req.GroupId,
        MemberName = req.MemberName,
        Endpoint   = req.Endpoint,
        P256DH     = req.P256DH,
        Auth       = req.Auth
    });
    return Results.Ok();
});

// The session id also goes into a server-set HttpOnly cookie. iOS Safari
// wipes localStorage and JS-written cookies after 7 days without a visit
// (ITP), which is why family members kept having to "log in" again; a
// cookie set by the server in a response is exempt from that cap.
app.MapPost("/api/session", (string id, HttpContext ctx) =>
{
    if (!Guid.TryParse(id, out _)) return Results.BadRequest();
    ctx.Response.Cookies.Append("club_session_srv", id, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromDays(365), Path = "/",
    });
    return Results.Ok();
}).DisableAntiforgery();
app.MapPost("/api/session/clear", (HttpContext ctx) => { ctx.Response.Cookies.Delete("club_session_srv"); return Results.Ok(); }).DisableAntiforgery();

// Personal mail/push link: marks the message as seen, logs the member in (server cookie, picked up by ClubAuthService)
// and opens the message. Unknown token -> front page, no hint whether it ever existed.
app.MapGet("/m/{token}", async (string token, TeamMessages messages, TeamAccess access, HttpContext ctx) =>
{
    var r = await messages.OpenLinkAsync(token);
    var group = r is null ? null : await access.GroupAsync(r.Message.GroupId);
    if (r is null || group is null) return Results.Redirect("/");
    var session = await access.StartSessionAsync(r.MemberId);
    ctx.Response.Cookies.Append("club_session_srv", session.Id.ToString(), new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365), Path = "/",   // Path: default would be /m only
    });
    return Results.Redirect($"/g/{group.Slug}/beskeder/{r.MessageId}");
});

// Invitation link: logs the member in and opens the team's home page.
app.MapGet("/l/{token}", async (string token, TeamAccess access, HttpContext ctx) =>
{
    var session = await access.LoginByLinkAsync(token);
    if (session is null) return Results.Redirect("/");
    ctx.Response.Cookies.Append("club_session_srv", session.Id.ToString(), new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365), Path = "/",
    });
    return Results.Redirect(ClubGroups.HomePath(session.Member.Group.Slug));
});

// Short team address: club.itmartin.dk/skelagerhoejen -> the team's home page (literal routes like /create still win).
app.MapGet("/{slug:regex(^[a-z0-9-]{{3,40}}$)}", async (string slug, TeamAccess access) =>
    await access.GroupAsync(slug) is not null ? Results.Redirect(ClubGroups.HomePath(slug)) : Results.NotFound());

app.MapGet("/api/push/key", (IPushNotifier push) => Results.Ok(push.PublicKey));

app.MapGet("/photo/{fileName}", (string fileName) =>
{
    var safeName = Path.GetFileName(fileName);
    var path = Path.Combine(photosRoot, safeName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

// Live-update attachments during Playing phase - unlike /photo this can be a
// short clip too, so it keeps its real extension and serves range-enabled
// (video needs seek support) with a mime picked from that extension.
app.MapGet("/media/{fileName}", (string fileName) =>
{
    var safeName = Path.GetFileName(fileName);
    var path = Path.Combine(liveMediaRoot, safeName);
    if (!File.Exists(path)) return Results.NotFound();
    var mime = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        _ => "image/jpeg"
    };
    return Results.File(path, mime, enableRangeProcessing: true);
});

app.MapRazorComponents<ITMartinSalgsveje.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

record ClubPushRequest(Guid GroupId, string MemberName, string Endpoint, string P256DH, string Auth);
