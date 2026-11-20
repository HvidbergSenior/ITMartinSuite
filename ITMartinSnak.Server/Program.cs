using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

// Snak (Kolibri Kommunikere) - 2026-10-09, user: "a quick app for 4 people where we can chat ... instead of
// using fb groups", push on by default, home-screen guide, Martin makes the tasks, the others take them.
// 2026-11-04 ROOMS: "a snak with every customer", "temporary chats, which will be removed after they are finished".
// One room per customer or job; the link /r/<key> is the way in; Martin (admin code) sees every room.
var builder = WebApplication.CreateBuilder(args);
// 2026-11-12 trin 2: files up to 90 MB (FileRules.MaxSize) - Kestrel's own limit is 30 MB. Cloudflare allows 100 MB.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = FileRules.MaxSize + 2 * 1024 * 1024);

builder.Services.AddKolibri(k =>
{
    k.Name = "Snak";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Ét rum til hver sag – chat og opgaver samlet ét sted, uden Facebook og uden reklamer.";
    k.Version = "2026.11.17.2";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        // Short and plain (user 2026-11-12: "The formål description need to be shorter and more understandable"); details live in HowTo.
        "Et lille, privat rum til én sag – kun for dem, du vælger.",
        "Skriv sammen, del billeder og hold styr på aftaler ét sted. Ingen reklamer, og intet bliver solgt.",
        "Når sagen er klaret, smelter rummet: det bliver slettet efter 14 dage.",
    ];
    // SHORT (user 2026-11-12 "snak kortere"): 5 steps, one line each.
    k.HowTo =
    [
        "Har du ikke et link? Skriv eller ring til Martin (31 19 47 30), så laver han et rum til dig – gratis.",
        "Åbn linket og skriv dit navn.",
        "Skriv i chatten. Tryk + for at sende et billede eller en fil.",
        "Slå beskeder til, så ved du, når der er svar.",
        "Under Opgaver skriver du kort, fx \"Tandlæge 13/11 kl 14\" – Snak finder selv dag og tid. Når den er klaret, skriver du kort hvordan, og den kan komme i Kalender.",
    ];
    // Snak's page is a static index.html, so /om and /hjaelp come as plain HTML (2026-10-19: they gave 404).
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Snak:DataDir"] ?? "/data")),
    ]);
});

// DDD/Clean since 2026-10-12: the endpoints below only call Application (Rooms, Group, Access).
var dataDir = builder.Configuration["Snak:DataDir"] ?? "/data";
var publicUrl = builder.Configuration["Snak:PublicUrl"] ?? "https://snak.itmartin.dk";
builder.Services.AddSnakApplication(builder.Configuration["Snak:AdminCode"] ?? "", builder.Configuration["Snak:ApiKey"] ?? "", publicUrl);
// Mail to customers (2026-11-10): the same one.com mailbox as Kunder - Snak__Smtp__* or else Kunder__Smtp__* from magic.env.
string? Smtp(string k) => builder.Configuration["Snak:Smtp:" + k] ?? builder.Configuration["Kunder:Smtp:" + k];
builder.Services.AddSnakInfrastructure(dataDir, new SmtpSettings(Smtp("Host") ?? "send.one.com",
    int.TryParse(Smtp("Port"), out var smtpPort) ? smtpPort : 587, Smtp("User") ?? "info@itmartin.dk", Smtp("Password"),
    Smtp("ReplyTo") ?? "ITMartin@Mensa.dk"));
builder.Services.AddHostedService<FinishedRoomsSweeper>();
builder.Services.AddHostedService<TaskReminders>();
builder.Services.AddHostedService<MailNudges>();

var app = builder.Build();
app.MapKolibri();
app.UseDefaultFiles();
// The page and the service worker must always be fetched fresh, or phones keep showing an old version
// (css/js carry ?v= in index.html, so they can be cached).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".html") || name == "snak-sw.js")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
// 2026-11-12 trin 2, two roles in every room (user: "Martin Hvidberg (user) + ITMartin (admin)"): Martin writes as himself,
// or as ITMartin when it is the company speaking (prices, "sagen er klaret"). Both count as the host (who waits for whom).
var HostName = builder.Configuration["Snak:HostName"] ?? "Martin Hvidberg";
var CompanyName = builder.Configuration["Snak:CompanyName"] ?? "ITMartin";
// The name the host had before (old messages and read marks say "Martin").
const string OldHostName = "Martin";
// Who writes: a guest as their name; the host as ITMartin (rolle "firma"), as their member name in a course group, else as Martin Hvidberg.
(string Name, bool Host) Writer(Room r, bool host, string? navn, string? rolle) =>
    !host ? (navn ?? "", false)
    : rolle == "firma" ? (CompanyName, true)
    : AsMember(r, navn) ? (navn!.Trim(), false)
    : (HostName, true);
bool IsAdmin(HttpContext c, Access a) => a.IsAdmin(c.Request.Cookies["snak_admin"]);
// In a room: Martin's code, or that room's own admin password (2026-11-08, e.g. Sonja's room).
bool IsRoomAdmin(HttpContext c, Access a, Room r) => a.IsRoomAdmin(r, c.Request.Cookies["snak_admin"], c.Request.Cookies["snak_ra_" + r.Id]);

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

static bool GoodPush(PushBody b) =>
    !string.IsNullOrWhiteSpace(b.Endpoint) && b.Endpoint.StartsWith("https://") && !string.IsNullOrWhiteSpace(b.P256dh) && !string.IsNullOrWhiteSpace(b.Auth);

// A room's page: the same index.html, never cached; the script reads the key from the address.
// 2026-11-08 (Vibz): the home-screen icon opened "/" (the shared manifest's start_url), and an iPhone home-screen app has its own
// storage, so it did not know the room -> "open your link". Now each room page points to its OWN manifest that starts in the room.
app.MapGet("/r/{key}", async (HttpContext c, string key, IWebHostEnvironment env, Rooms rooms) =>
{
    c.Response.Headers.CacheControl = "no-cache";
    var html = await File.ReadAllTextAsync(Path.Combine(env.WebRootPath, "index.html"));
    if (rooms.ByKey(key) is { } r)
        html = html
            .Replace("href=\"/manifest.webmanifest\"", $"href=\"/r/{r.Key}/manifest.webmanifest\"")
            .Replace("name=\"apple-mobile-web-app-title\" content=\"Snak\"", $"name=\"apple-mobile-web-app-title\" content=\"{System.Net.WebUtility.HtmlEncode(r.Title)}\"");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/r/{key}/manifest.webmanifest", (HttpContext c, string key, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    c.Response.Headers.CacheControl = "no-cache";
    var icon = "/_content/ITMartin.Shared.UI/kolibri-icon.svg";
    return Results.Json(new
    {
        id = $"/r/{r.Key}", name = r.Title + " · Snak", short_name = r.Title.Length > 12 ? "Snak" : r.Title,
        start_url = $"/r/{r.Key}", scope = "/", display = "standalone",
        background_color = "#f4f6f5", theme_color = "#f4f6f5", lang = "da",
        icons = new object[]
        {
            new { src = icon, sizes = "any", type = "image/svg+xml", purpose = "any" },
            new { src = icon, sizes = "any", type = "image/svg+xml", purpose = "maskable" },
        },
    }, contentType: "application/manifest+json");
});

// The easy address (2026-11-08): snak.itmartin.dk/sonja -> the room. Literal routes (/om, /api/...) always win over this one.
app.MapGet("/{slug:regex(^[a-z0-9][a-z0-9-]{{1,29}}$)}", (string slug, Rooms rooms) =>
    rooms.BySlug(slug) is { } r ? Results.Redirect($"/r/{r.Key}") : Results.NotFound());

// ---- Martin ----

app.MapPost("/api/login", (HttpContext c, LoginBody b, Access access) =>
{
    var result = access.Login(b.Kode, ClientIp(c));
    if (result == LoginResult.Locked)
        return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);
    if (result == LoginResult.Wrong)
        return Results.Json(new { fejl = "Koden passer ikke." }, statusCode: 401);
    c.Response.Cookies.Append("snak_admin", access.AdminToken,
        new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) });
    return Results.Ok(new { admin = true });
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    c.Response.Cookies.Delete("snak");
    c.Response.Cookies.Delete("snak_admin");
    return Results.Ok();
});

app.MapGet("/api/me", (HttpContext c, Access access, IPushSender push) =>
    Results.Ok(new { admin = IsAdmin(c, access), pushKey = push.PublicKey, vaert = HostName, firma = CompanyName }));

app.MapGet("/api/rum", (HttpContext c, Access access, Rooms rooms) =>
    IsAdmin(c, access)
        ? Results.Json(rooms.Overview().Select(r => new
        {
            r.Id, r.Title, r.CustomerRef, r.CreatedAt, r.LastAt, r.DoneAt, r.DeleteAt, r.Waiting, r.LastName, r.LastText, r.Members, r.HasCode,
            Link = $"{publicUrl}/r/{r.Key}", r.Slug, r.Gruppe,
        }), web)
        : Results.Unauthorized());

// Martin makes a room - or Kunder / itmartin.dk with the API key (header X-Snak-Key), optionally with the first message.
app.MapPost("/api/rum", (HttpContext c, RoomBody b, Access access, Rooms rooms) =>
{
    if (!IsAdmin(c, access) && !access.IsApi(c.Request.Headers["X-Snak-Key"].FirstOrDefault())) return Results.Unauthorized();
    try
    {
        // Easy-Pass trip rooms (2026-11-18): tasks on days + a melt day, through the API key only.
        var r = b.Opgaver is { Count: > 0 } || !string.IsNullOrWhiteSpace(b.SlutDag)
            ? rooms.CreatePlanned(b.Titel, b.Kunde, b.Medlemmer, (b.Opgaver ?? []).Select(o => (o.Titel ?? "", o.Note ?? "", o.Dag ?? "", o.Hvornaar ?? "")), b.SlutDag)
            : rooms.Create(b.Titel, b.Kunde, b.Medlemmer, b.Kode, b.Adresse, b.Gruppe);
        if (!string.IsNullOrWhiteSpace(b.Besked)) rooms.GroupOf(r).Write(string.IsNullOrWhiteSpace(b.Navn) ? "Kunde" : b.Navn, b.Besked);
        return Results.Json(new { id = r.Id, titel = r.Title, link = rooms.Link(r), nemLink = rooms.EasyLink(r) }, web);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { fejl = ex.Message }); }
});

// Kunder (2026-11-10): mail the room link to the customer, and keep the address for "Martin har skrevet" mails.
app.MapPost("/api/rum/inviter", async (HttpContext c, InviteBody b, Access access, RoomMails mails, CancellationToken ct) =>
{
    if (!IsAdmin(c, access) && !access.IsApi(c.Request.Headers["X-Snak-Key"].FirstOrDefault())) return Results.Unauthorized();
    var error = await mails.InviteAsync(b.Link, b.Mail, b.Navn, ct);
    return error is null ? Results.Ok(new { sendt = true }) : Results.BadRequest(new { fejl = error });
});

app.MapPost("/api/rum/{id}/klaret", (HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.MarkDone(id, CompanyName) ? Results.Ok() : Results.NotFound());

app.MapPost("/api/rum/{id}/genaabn", (HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Reopen(id) ? Results.Ok() : Results.NotFound());

app.MapPut("/api/rum/{id}", (HttpContext c, string id, RoomBody b, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Rename(id, b.Titel) ? Results.Ok() : Results.NotFound());

// The short address, set later (2026-11-20). Martin only.
app.MapPut("/api/rum/{id}/adresse", (HttpContext c, string id, SlugBody b, Access access, Rooms rooms) =>
{
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    if (rooms.SetSlug(id, b.Adresse) is { } err) return Results.BadRequest(new { fejl = err });
    var r = rooms.Overview().FirstOrDefault(x => x.Id == id);
    return Results.Json(new { adresse = r?.Slug ?? "" }, web);
});

// Only the chat, or the chat with Opgaver and Filer (2026-11-17). Martin only.
app.MapPut("/api/rum/{id}/kunchat", (HttpContext c, string id, KunChatBody b, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.SetKunChat(id, b.KunChat) ? Results.Ok() : Results.NotFound());

// A group's people ("Navn – hvad de laver", one per line) and topics (one per line). Martin only.
app.MapPut("/api/rum/{id}/holdet", (HttpContext c, string id, RosterBody b, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.SetRoster(id, b.Medlemmer, b.Emner, b.Links, b.Kalender) ? Results.Ok() : Results.NotFound());

app.MapDelete("/api/rum/{id}",(HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Delete(id) ? Results.Ok() : Results.NotFound());

// Martin's phone gets a push from every room.
app.MapPost("/api/push", (HttpContext c, PushBody b, Access access, IPushSender push) =>
{
    if (!IsAdmin(c, access)) return Results.Unauthorized();
    if (!GoodPush(b)) return Results.BadRequest();
    push.Subscribe("", b.Endpoint!, b.P256dh!, b.Auth!, HostName);
    return Results.Ok();
});

app.MapPost("/api/push/fra", (PushBody b, IPushSender push) =>
{
    if (!string.IsNullOrWhiteSpace(b.Endpoint)) push.Unsubscribe(b.Endpoint);
    return Results.Ok();
});

// ---- one room: the key in the address is the way in ----

var room = app.MapGroup("/api/r/{key}");

// 2026-11-12 the door of a family room (DoorRules): only a phone that holds one of the names gets past the door; everything it does
// is done under THAT name (whatever the page sends). The room's admin always gets in. Course groups and customer rooms: as before.
room.AddEndpointFilter(async (ctx, next) =>
{
    var c = ctx.HttpContext;
    var key = c.Request.RouteValues["key"] as string;
    var rooms = c.RequestServices.GetRequiredService<Rooms>();
    if (rooms.ByKey(key) is { } r && DoorRules.Locked(r) && !IsRoomAdmin(c, c.RequestServices.GetRequiredService<Access>(), r))
    {
        var rest = (c.Request.Path.Value ?? "")[("/api/r/" + key).Length..].TrimEnd('/');
        if (rest is not ("" or "/doer" or "/admin"))
        {
            if (DoorRules.NameOf(r, DeviceHash(c)) is not { } held)
                return Results.Json(new { fejl = "Tryk på dit navn ved døren først.", doer = true }, statusCode: 403);
            c.Items["snak_navn"] = held;
        }
    }
    return await next(ctx);
});

room.MapGet("", (HttpContext c, string key, Rooms rooms, Access access, IPushSender push) =>
    rooms.ByKey(key) is { } r
        ? Results.Json(new
        {
            id = r.Id, titel = r.Title, klaret = r.IsDone, sletDato = r.DeleteAt, admin = IsRoomAdmin(c, access, r), alleRum = IsAdmin(c, access),
            pushKey = push.PublicKey, vaert = HostName, firma = CompanyName, medlemmer = r.Members ?? [], harKode = r.HasCode,
            gruppe = r.Gruppe, om = r.About ?? [], emner = r.Topics ?? [], links = r.Links ?? [],
            harKalender = !string.IsNullOrEmpty(r.CalendarUrl), kalender = IsAdmin(c, access) ? r.CalendarUrl ?? "" : "", adresse = r.Slug,
            laast = DoorRules.Locked(r), mitNavn = DoorRules.NameOf(r, DeviceHash(c)), kunChat = r.KunChat,
        }, web)
        : Results.Json(new { fejl = "Rummet findes ikke (mere)." }, statusCode: 404));

// The door (2026-11-12): the phone taps a name. First phone on a name takes it; another phone waits for the admin ("venter").
room.MapPost("/doer", (HttpContext c, string key, DoorBody b, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!DoorRules.Locked(r)) return Results.Json(new { resultat = "ind", navn = GroupRules.CleanName(b.Navn) }, web);
    var token = c.Request.Cookies["snak_d"];
    if (string.IsNullOrEmpty(token) || token.Length < 20)
    {
        token = DoorRules.NewToken();
        c.Response.Cookies.Append("snak_d", token,
            new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400), Path = "/" });
    }
    var result = rooms.Knock(r.Id, b.Navn, DoorRules.Hash(token), b.Enhed);
    return Results.Json(new
    {
        resultat = result switch { DoorResult.In => "ind", DoorResult.Waiting => "venter", _ => "ikke-paa-listen" },
        navn = DoorRules.MemberName(r, b.Navn),
    }, web);
});

// The admin approves or turns away a waiting phone, or frees a name.
room.MapPost("/doer/godkend", (HttpContext c, string key, DoorAdminBody b, Rooms rooms, Access access) =>
    rooms.ByKey(key) is not { } r ? Results.NotFound() : !IsRoomAdmin(c, access, r) ? Results.StatusCode(403)
        : rooms.ApproveDevice(r.Id, b.Id ?? "") ? Results.Ok() : Results.NotFound());
room.MapPost("/doer/afvis", (HttpContext c, string key, DoorAdminBody b, Rooms rooms, Access access) =>
    rooms.ByKey(key) is not { } r ? Results.NotFound() : !IsRoomAdmin(c, access, r) ? Results.StatusCode(403)
        : rooms.RejectDevice(r.Id, b.Id ?? "") ? Results.Ok() : Results.NotFound());
room.MapPost("/doer/frigiv", (HttpContext c, string key, DoorAdminBody b, Rooms rooms, Access access) =>
    rooms.ByKey(key) is not { } r ? Results.NotFound() : !IsRoomAdmin(c, access, r) ? Results.StatusCode(403)
        : rooms.ReleaseName(r.Id, b.Navn) ? Results.Ok() : Results.NotFound());

// The room's own admin password (2026-11-08): admin in this room only, remembered on this phone.
room.MapPost("/admin", (HttpContext c, string key, LoginBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r || !r.HasCode) return Results.NotFound();
    var result = access.RoomLogin(r, b.Kode, ClientIp(c));
    if (result == LoginResult.Locked)
        return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);
    if (result == LoginResult.Wrong)
        return Results.Json(new { fejl = "Koden passer ikke." }, statusCode: 403);
    c.Response.Cookies.Append("snak_ra_" + r.Id, r.CodeHash,
        new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) });
    return Results.Ok(new { admin = true });
});

room.MapGet("/beskeder", (string key, Rooms rooms, long? efter) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).MessagesAfter(efter ?? 0), web) : Results.NotFound());

room.MapPost("/beskeder", (HttpContext c, string key, MessageBody b, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var (name, asHost) = Writer(r, IsRoomAdmin(c, access, r), Who(c, b.Navn), b.Rolle);
    var m = rooms.GroupOf(r).Write(name, b.Tekst, b.Endpoint, asHost, b.SvarPaa, b.Fil);
    return m is null ? Results.BadRequest(new { fejl = "Skriv dit navn og en besked." }) : Results.Json(m, web);
});

// Trin 2: a reaction (one of six) - tap again to take it back.
room.MapPost("/beskeder/{id:long}/reaktion", (HttpContext c, string key, long id, ReactBody b, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var (name, _) = Writer(r, IsRoomAdmin(c, access, r), Who(c, b.Navn), b.Rolle);
    var m = rooms.GroupOf(r).React(id, b.Emoji, name);
    return m is null ? Results.BadRequest() : Results.Json(m, web);
});

// Trin 2: search the whole history of the room.
room.MapGet("/soeg", (string key, string? q, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Search(q), web) : Results.NotFound());

// Seen (2026-11-09): the page says how far it has read and how the phone is set up. Everyone gets the read positions;
// only the room's admin (Martin or the tovholder) sees the setup view.
room.MapGet("/set", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).ReadPositions(), web) : Results.NotFound());

room.MapPost("/set", (HttpContext c, string key, SeenBody b, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var name = Writer(r, IsRoomAdmin(c, access, r), Who(c, b.Navn), null).Name;
    return rooms.GroupOf(r).MarkSeen(name, b.Besked, b.Enhed, b.Hjem, b.Push, b.InApp) is null ? Results.BadRequest() : Results.Ok();
});

room.MapGet("/opsaetning", (HttpContext c, string key, Rooms rooms, Access access, IPushSender push) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsRoomAdmin(c, access, r)) return Results.Json(new { fejl = "Kun for tovholderen." }, statusCode: 403);
    var g = rooms.GroupOf(r);
    var seen = g.SeenList().ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    var phones = push.PhonesPerName(r.Id);
    var names = (r.Members ?? []).Concat(seen.Keys).Concat(phones.Keys)
        .Where(n => !string.Equals(n, HostName, StringComparison.OrdinalIgnoreCase) && !string.Equals(n, CompanyName, StringComparison.OrdinalIgnoreCase)
            && !(string.Equals(n, OldHostName, StringComparison.OrdinalIgnoreCase) && !(r.Members ?? []).Contains(n, StringComparer.OrdinalIgnoreCase)))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var rows = names.Select(n =>
    {
        seen.TryGetValue(n, out var s);
        return new
        {
            navn = n, medlem = (r.Members ?? []).Contains(n, StringComparer.OrdinalIgnoreCase),
            sidstInde = s?.At, laest = s?.MessageId ?? 0, enhed = s?.Device ?? "", hjem = s?.HomeScreen ?? false,
            push = s?.Push ?? "", inApp = s?.InApp ?? false, telefoner = phones.TryGetValue(n, out var p) ? p : 0,
        };
    });
    var door = DoorRules.Locked(r) ? new
    {
        telefoner = (r.Members ?? []).ToDictionary(m => m, m => r.Claims is { } cl && cl.TryGetValue(m, out var d) ? d.Count : 0),
        venter = (r.Pending ?? []).Select(p => new { id = p.Id, navn = p.Name, enhed = p.Kind, at = p.At }),
    } : null;
    return Results.Json(new { sidsteBesked = g.Last?.Id ?? 0, folk = rows, doer = door }, web);
});

// The course's own calendar (2026-11-09): the room's .ics feed, read through Calendars (kept 20 min). Danish dates on the page.
room.MapGet("/kalender", async (string key, Rooms rooms, Calendars cal, CancellationToken ct) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var events = await cal.EventsAsync(r.CalendarUrl, ct);
    return Results.Json(events.Select(e => new { start = e.StartUtc, slut = e.EndUtc, titel = e.Title, sted = e.Place, heleDagen = e.AllDay }), web);
});

// Profiles: each participant's own card (mail, phone, website, LinkedIn, about). Only the room sees them.
room.MapGet("/profiler", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Profiles(), web) : Results.NotFound());

room.MapPut("/profil", (HttpContext c, string key, ProfileBody b, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var (p, err) = rooms.GroupOf(r).SaveProfile(Who(c, b.Navn), b.Mail, b.Telefon, b.Web, b.LinkedIn, b.Om);
    return p is null ? Results.BadRequest(new { fejl = err }) : Results.Json(p, web);
});

// "Til næste gang": each person's own ticks (only they ask for their own name).
room.MapGet("/mine", (HttpContext c, string key, string? navn, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).MyDone(Who(c, navn)), web) : Results.NotFound());

room.MapPost("/opgaver/{id:long}/mig", (HttpContext c, string key, long id, MineBody b, Rooms rooms) =>
    rooms.ByKey(key) is not { } r ? Results.NotFound()
        : rooms.GroupOf(r).SetMyDone(Who(c, b.Navn), id, b.Gjort) is { } ids ? Results.Json(ids, web) : Results.BadRequest());

// Tasks: everyone in the room sees them and can put themselves on one; only Martin makes, edits and removes.
room.MapGet("/opgaver", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Tasks(), web) : Results.NotFound());

room.MapGet("/idag", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(new { dag = rooms.GroupOf(r).Today.ToString("yyyy-MM-dd") }, web) : Results.NotFound());

// 2026-11-12: in a shared room (named people, not a course group) everyone adds dates and tasks themselves, and changes or
// removes their own; the host still does everything.
room.MapPost("/opgaver", (HttpContext c, string key, TaskBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var host = IsRoomAdmin(c, access, r);
    if (!host && !SharedMember(r, Who(c, b.Navn))) return Results.StatusCode(403);
    var t = rooms.GroupOf(r).AddTask(b.Titel, b.Note, b.Hvornaar, b.Endpoint, b.Til, b.Dag, b.HverDag ?? false,
        host ? null : Who(c, b.Navn), b.HvertAar ?? false, b.Slut, b.Sted);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven skal have en titel." }) : Results.Json(t, web);
});

room.MapPut("/opgaver/{id:long}", (HttpContext c, string key, long id, TaskBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var host = IsRoomAdmin(c, access, r);
    if (!host && !(SharedMember(r, Who(c, b.Navn)) && rooms.GroupOf(r).MayChangeTask(id, Who(c, b.Navn), false))) return Results.StatusCode(403);
    var t = rooms.GroupOf(r).EditTask(id, b.Titel, b.Note, b.Hvornaar, b.Til, b.Endpoint, b.Dag, b.HverDag, b.HvertAar, b.Slut, b.Sted);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven findes ikke, eller titlen mangler." }) : Results.Json(t, web);
});

room.MapPost("/opgaver/{id:long}/faerdig", (HttpContext c, string key, long id, DoneBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    // 2026-11-17: in a shared room (named people) everyone closes tasks and says how ("Vaccination d. 17.11"); the host as before.
    var host = IsRoomAdmin(c, access, r);
    if (!host && !SharedMember(r, Who(c, b.Navn))) return Results.StatusCode(403);
    var by = Who(c, b.Navn) is { Length: > 0 } n ? n : host ? "Martin" : "";
    return rooms.GroupOf(r).SetDone(id, b.Faerdig, b.Hvordan, by, b.Kalender ?? false, b.NaesteGang, b.Endpoint) is null ? Results.NotFound() : Results.Ok();
});

room.MapDelete("/opgaver/{id:long}", (HttpContext c, string key, long id, string? navn, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsRoomAdmin(c, access, r) && !(SharedMember(r, Who(c, navn)) && rooms.GroupOf(r).MayChangeTask(id, Who(c, navn), false))) return Results.StatusCode(403);
    return rooms.GroupOf(r).DeleteTask(id) ? Results.Ok() : Results.NotFound();
});

room.MapPost("/opgaver/{id:long}/tag", (HttpContext c, string key, long id, TakeBody b, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var t = rooms.GroupOf(r).ToggleTaker(id, Who(c, b.Navn), b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Skriv dit navn først." }) : Results.Json(t, web);
});

// Documents: everyone in the room adds and downloads; the one who added it (same name) or Martin removes it.
room.MapGet("/filer", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Files().Select(FileView), web) : Results.NotFound());

// multipart/form-data: fil + navn (+ endpoint). The form is read by hand, so no antiforgery token is needed (same as the
// JSON endpoints - the room key in the address is the access).
room.MapPost("/filer", async (HttpContext c, string key, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!c.Request.HasFormContentType) return Results.BadRequest(new { fejl = "Vælg en fil." });
    var form = await c.Request.ReadFormAsync(c.RequestAborted);
    if (form.Files.GetFile("fil") is not { } f) return Results.BadRequest(new { fejl = "Vælg en fil." });
    var (who, _) = Writer(r, IsRoomAdmin(c, access, r), Who(c, form["navn"].ToString()), form["rolle"].ToString());
    await using var s = f.OpenReadStream();
    // "chat" = sent from the chat: the chat message gives the push.
    var (file, err) = await rooms.GroupOf(r).AddFileAsync(f.FileName, f.Length, s, who,
        form["endpoint"].ToString(), c.RequestAborted, RosterRules.TopicFor(r.Topics, form["emne"].ToString()), push: form["chat"] != "1");
    return file is null ? Results.BadRequest(new { fejl = err }) : Results.Json(FileView(file), web);
});

room.MapGet("/filer/{id:long}", (HttpContext c, string key, long id, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var g = rooms.GroupOf(r);
    if (g.File(id) is not { } f || g.OpenFile(f) is not { } s) return Results.NotFound();
    c.Response.Headers.XContentTypeOptions = "nosniff";
    c.Response.Headers.CacheControl = "private, max-age=3600";
    // PDF and pictures open in the phone's viewer; everything else is saved.
    var inline = f.ContentType == "application/pdf" || f.ContentType.StartsWith("image/");
    var disp = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(inline ? "inline" : "attachment");
    disp.SetHttpFileName(f.Name);
    c.Response.Headers.ContentDisposition = disp.ToString();
    return Results.Stream(s, f.ContentType, enableRangeProcessing: true);
});

room.MapDelete("/filer/{id:long}", (HttpContext c, string key, long id, string? navn, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    return rooms.GroupOf(r).DeleteFile(id, Who(c, navn), IsRoomAdmin(c, access, r)) ? Results.Ok() : Results.StatusCode(403);
});

// Idéer (2026-11-20): paste a link or a text (from a mail, a web page) - it becomes a card everyone in the room sees.
room.MapGet("/ideer", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Ideas(), web) : Results.NotFound());

room.MapPost("/ideer", async (HttpContext c, string key, IdeaBody b, Rooms rooms, Access access, IdeaIntake intake) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var (who, _) = Writer(r, IsRoomAdmin(c, access, r), Who(c, b.Navn), b.Rolle);
    var (idea, err) = await intake.AddAsync(rooms.GroupOf(r), who, b.Tekst, b.Endpoint, c.RequestAborted);
    return idea is null ? Results.BadRequest(new { fejl = err }) : Results.Json(idea, web);
});

room.MapDelete("/ideer/{id:long}", (HttpContext c, string key, long id, string? navn, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    return rooms.GroupOf(r).DeleteIdea(id, Who(c, navn), IsRoomAdmin(c, access, r)) ? Results.Ok() : Results.StatusCode(403);
});

// Same rule as adding an Opgave: the host, or a named person in a shared room.
room.MapPost("/ideer/{id:long}/opgave", (HttpContext c, string key, long id, IdeaBody b, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var host = IsRoomAdmin(c, access, r);
    if (!host && !SharedMember(r, Who(c, b.Navn))) return Results.StatusCode(403);
    var t = rooms.GroupOf(r).IdeaToTask(id, host ? null : Who(c, b.Navn), b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Idéen findes ikke, eller den er allerede en opgave." }) : Results.Json(t, web);
});

room.MapGet("/live",async (HttpContext c, string key, Rooms rooms, ILiveFeed live) =>
{
    if (rooms.ByKey(key) is not { } r) { c.Response.StatusCode = 404; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join(r.Id);
    try
    {
        await c.Response.WriteAsync("event: hej\ndata: {}\n\n", c.RequestAborted);
        await c.Response.Body.FlushAsync(c.RequestAborted);
        while (!c.RequestAborted.IsCancellationRequested)
        {
            // A comment line every 20 s keeps Cloudflare from closing a quiet line (~100 s limit).
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
            wait.CancelAfter(TimeSpan.FromSeconds(20));
            string line;
            try { line = await reader.ReadAsync(wait.Token); }
            catch (OperationCanceledException) when (!c.RequestAborted.IsCancellationRequested) { line = ": ping\n\n"; }
            await c.Response.WriteAsync(line, c.RequestAborted);
            await c.Response.Body.FlushAsync(c.RequestAborted);
        }
    }
    catch (OperationCanceledException) { }
    finally { live.Leave(id); }
});

room.MapPost("/push", (HttpContext c, string key, PushBody b, Rooms rooms, IPushSender push) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!GoodPush(b)) return Results.BadRequest();
    push.Subscribe(r.Id, b.Endpoint!, b.P256dh!, b.Auth!, GroupRules.CleanName(Who(c, b.Navn)));
    return Results.Ok();
});

app.Run();

// 2026-11-09: in a group (Hold 36) Martin is one of the participants - he writes under his own name on the list
// ("Martin Hvidberg"), not as the host. Anywhere else the room's admin writes as the host.
static bool AsMember(Room r, string? name) =>
    r.Gruppe && !string.IsNullOrWhiteSpace(name) && (r.Members ?? []).Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

// One of the named people in a shared room that is not a course group (family, Sonja's circle) - they add their own dates.
static bool SharedMember(Room r, string? name) =>
    !r.Gruppe && !string.IsNullOrWhiteSpace(name) && (r.Members ?? []).Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

// In a family room the name is always the one this phone holds (set by the door filter), whatever the page sends.
static string? Who(HttpContext c, string? sent) => c.Items["snak_navn"] as string ?? sent;

// The phone's door token (cookie) as the hash the room keeps.
static string? DeviceHash(HttpContext c) =>
    c.Request.Cookies["snak_d"] is { Length: >= 20 } t ? DoorRules.Hash(t) : null;

// What the page sees of a file - never the name on disk.
static object FileView(SharedFile f) => new { f.Id, f.Name, f.Size, f.ContentType, f.By, f.At, f.Topic };

public sealed record LoginBody(string? Kode);
public sealed record SlugBody(string? Adresse);
public sealed record IdeaBody(string? Navn, string? Tekst, string? Endpoint, string? Rolle = null);
public sealed record RoomBody(string? Titel, string? Kunde, string? Navn, string? Besked, string? Medlemmer = null, string? Kode = null, string? Adresse = null,
    bool Gruppe = false, List<PlannedTaskBody>? Opgaver = null, string? SlutDag = null);
public sealed record PlannedTaskBody(string? Titel, string? Note, string? Dag, string? Hvornaar);
public sealed record KunChatBody(bool KunChat);
public sealed record InviteBody(string? Link, string? Mail, string? Navn);
public sealed record RosterBody(string? Medlemmer, string? Emner, string? Links = null, string? Kalender = null);
public sealed record MessageBody(string? Navn, string? Tekst, string? Endpoint, long? SvarPaa = null, long? Fil = null, string? Rolle = null);
public sealed record DoorBody(string? Navn, string? Enhed);
public sealed record DoorAdminBody(string? Id, string? Navn);
public sealed record ReactBody(string? Navn, string? Emoji, string? Rolle = null);
public sealed record TaskBody(string? Titel, string? Note, string? Hvornaar, string? Endpoint, string? Til = null, string? Dag = null, bool? HverDag = null,
    string? Navn = null, bool? HvertAar = null, string? Slut = null, string? Sted = null);
public sealed record DoneBody(bool Faerdig, string? Hvordan = null, bool? Kalender = null, string? NaesteGang = null, string? Navn = null,
    string? Endpoint = null);
public sealed record TakeBody(string? Navn, string? Endpoint);
public sealed record ProfileBody(string? Navn, string? Mail, string? Telefon, string? Web, string? LinkedIn, string? Om);
public sealed record MineBody(string? Navn, bool Gjort);
public sealed record SeenBody(string? Navn, long Besked, string? Enhed, bool Hjem, string? Push, bool InApp);
public sealed record PushBody(string? Endpoint, string? P256dh, string? Auth, string? Navn);

// Every hour: rooms marked "klaret" more than 14 days ago are deleted (messages, tasks, phones).
public sealed class FinishedRoomsSweeper(Rooms rooms, ILogger<FinishedRoomsSweeper> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                var m = rooms.MeltDue(); if (m > 0) log.LogInformation("Melted {Count} rooms past their last day", m);
                var n = rooms.DeleteFinished(); if (n > 0) log.LogInformation("Deleted {Count} finished rooms", n);
            }
            catch (Exception ex) { log.LogWarning(ex, "Deleting finished rooms failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

// Every minute: a push to the room's phones 60 and 30 minutes before a task's clock time today (2026-11-08).
public sealed class TaskReminders(Rooms rooms, ILogger<TaskReminders> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { var n = rooms.SendReminders(); if (n > 0) log.LogInformation("Sent {Count} task reminders", n); }
            catch (Exception ex) { log.LogWarning(ex, "Task reminders failed"); }
        }
    }
}

// Every minute (2026-11-10): one mail to a customer who has not read Martin's newest message and gets no beskeder.
public sealed class MailNudges(RoomMails mails, ILogger<MailNudges> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { var n = await mails.NudgeAsync(ct); if (n > 0) log.LogInformation("Sent {Count} 'Martin har skrevet' mails", n); }
            catch (Exception ex) { log.LogWarning(ex, "Mail nudges failed"); }
        }
    }
}

public partial class Program;
