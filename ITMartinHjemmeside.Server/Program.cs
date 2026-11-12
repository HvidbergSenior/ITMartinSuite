using ITMartin.Shared.UI.Kolibri;
using ITMartinHjemmeside.Application;
using ITMartinHjemmeside.Domain;
using ITMartinHjemmeside.Infrastructure;
using ITMartinHjemmeside.Server;

// Kolibri Hjemmeside - 2026-11-09, user: "If someone wants me to help with their homepage like ITMartin.dk, bogshoppen.dk",
// packages Selv / Sammen / Fuld hjælp, "they get an app where they can change things", "an easy way for me to help them set up
// ... both one for me and one for user", "an overview of the homepages and info". ONE app with two doors:
//   hjemmeside.itmartin.dk        Martin: overview of all sites, Ny hjemmeside, set-up, edit any site, zip
//   <customer domain>/            the customer's site (server-made HTML)
//   <customer domain>/ret         the owner's door: only their site, big buttons, Fortryd
//   hjemmeside.itmartin.dk/s/<id>/  preview before the domain points here
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Hjemmeside";
    k.KolibriName = "Kolibri Hjemmeside";
    k.Family = "rede";
    k.Tagline = "Din egen hjemmeside – du retter selv tekst og billeder, eller Martin gør det for dig.";
    k.Version = "2026.11";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        // Short and plain (user 2026-11-12: "The formål description need to be shorter and more understandable"); details live in HowTo.
        "En hjemmeside, der ligner dig – lavet til dig, ikke en skabelon.",
        "Intet abonnement: første side er gratis, og bagefter betaler du kun for de uger, den er tændt.",
        "Den ligger på min server i Danmark med backup hver nat – og domænet er dit.",
    ];
    k.HowTo =
    [
        "Gå til din side og skriv /ret efter adressen (fx minside.dk/ret). Skriv din kode.",
        "Tryk på en side for at åbne den. Ret teksten i boksen og tryk Gem. Billeder: tryk Skift billede eller Tilføj billede – du kan tage det med telefonen med det samme.",
        "Gik noget galt? Tryk Fortryd – så er siden som før. Du kan fortryde mange skridt tilbage.",
        "Læg Ret siden på telefonen, så åbner den som en app.",
    ];
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Hjemmeside:DataDir"] ?? "/data")),
    ]);
});

var dataDir = builder.Configuration["Hjemmeside:DataDir"] ?? "/data";
var publicUrl = (builder.Configuration["Hjemmeside:PublicUrl"] ?? "https://hjemmeside.itmartin.dk").TrimEnd('/');
builder.Services.AddHjemmesideApplication(builder.Configuration["Hjemmeside:AdminCode"] ?? "");
builder.Services.AddHjemmesideInfrastructure(dataDir);
// The pages send packages as "Selv"/"Sammen"/"Fuld".
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

// A customer's own domain: everything except the app's own paths is that site, so /om-os becomes /s/<id>/om-os inside.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    if (!IsAppPath(path) && ctx.RequestServices.GetRequiredService<Sites>().ByHost(ctx.Request.Host.Host) is { } site)
    {
        ctx.Items["egetDomaene"] = site.Id;
        ctx.Request.Path = "/s/" + site.Id + (path == "/" ? "/" : path);
    }
    await next();
});
// Routing must come AFTER the rewrite above, or the endpoint is chosen from the old path (customer domain -> 404).
app.UseRouting();

app.MapKolibri();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html")) ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

const string MartinCookie = "hj_m";
static string OwnerCookie(string id) => "hj_e_" + id;
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";
static bool IsAppPath(string p) =>
    p.StartsWith("/api/") || p.StartsWith("/_content/") || p == "/ret" || p.StartsWith("/app.") || p == "/kolibri-sw.js" || p == "/health";

bool IsMartin(HttpContext c, Access a) => a.IsMartin(c.Request.Cookies[MartinCookie]);
bool MayEdit(HttpContext c, Access a, Site s) => a.MayEdit(s, c.Request.Cookies[MartinCookie], c.Request.Cookies[OwnerCookie(s.Id)]);
string PreviewUrl(Site s) => $"{publicUrl}/s/{s.Id}/";
string? LiveUrl(Site s) => s.Hosts.Count > 0 ? "https://" + s.Hosts[0] + "/" : null;

void SetCookie(HttpContext c, string name, string value) =>
    c.Response.Cookies.Append(name, value, new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) });

// Every API call: a broken rule becomes a Danish message for the page, never a 500.
IResult Try(Func<IResult> work)
{
    try { return work(); }
    catch (SiteException ex) { return Results.BadRequest(new { fejl = ex.Message }); }
}
async Task<IResult> TryAsync(Func<Task<IResult>> work)
{
    try { return await work(); }
    catch (SiteException ex) { return Results.BadRequest(new { fejl = ex.Message }); }
}

// What the door page needs: which site this door belongs to (own domain or ?side=), and who is logged in.
object Overview(Site s, Sites sites, Editor editor) => new
{
    id = s.Id, navn = s.Name, pakke = s.Package.ToString(), pakkeNavn = SiteRules.PackageName(s.Package),
    pris = s.Price ?? SiteRules.NormalPrice(s.Package), normalPris = SiteRules.NormalPrice(s.Package), egenPris = s.Price is not null,
    domaener = s.Hosts, preview = PreviewUrl(s), live = LiveUrl(s), aendret = s.ChangedAt, oprettet = s.CreatedAt,
    sider = s.Pages.Count, harKode = s.OwnerCodeHash is not null, ejerRetter = s.OwnerMayEdit,
    brugt = sites.UsedBytes(s.Id), plads = SiteRules.Quota(s.Package), note = s.Note, fortryd = editor.UndoSteps(s.Id),
};

// ---- the customer's site (public) ----

app.MapGet("/s/{id}/{slug?}", (HttpContext c, string id, string? slug, Sites sites) =>
{
    if (sites.Get(id) is not { } site) return Results.NotFound();
    var page = site.Pages.FirstOrDefault(p => p.Slug == (slug ?? SiteRules.FrontSlug));
    if (page is null) return Results.Content("<!DOCTYPE html><meta charset=utf-8><p>Siden findes ikke. <a href=\"./\">Til forsiden</a></p>", "text/html; charset=utf-8", statusCode: 404);
    var basePath = c.Items.ContainsKey("egetDomaene") ? "" : "/s/" + site.Id;
    c.Response.Headers.CacheControl = "no-cache";
    return Results.Content(SiteHtml.Page(site, page, basePath), "text/html; charset=utf-8");
});

app.MapGet("/s/{id}/billeder/{name}", (HttpContext c, string id, string name, IPictureStore pictures) =>
{
    if (pictures.Open(id, name) is not { } stream) return Results.NotFound();
    // Names are new for every picture, so a phone may keep them for a year.
    c.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.Stream(stream, "image/jpeg");
});

// The owner's door on their own domain - the same page as Martin's door, it finds the site from the address.
app.MapGet("/ret", (IWebHostEnvironment env, HttpContext c) =>
{
    c.Response.Headers.CacheControl = "no-cache";
    return Results.File(Path.Combine(env.WebRootPath, "index.html"), "text/html; charset=utf-8");
});

// Each door its own home-screen app: "Ret <site>" opens /ret on the customer's domain, Martin's opens the overview.
app.MapGet("/app.webmanifest", (HttpContext c, Sites sites) =>
{
    var site = sites.ByHost(c.Request.Host.Host);
    var icon = "/_content/ITMartin.Shared.UI/kolibri-icon.svg";
    return Results.Json(new
    {
        name = site is null ? "Kolibri Hjemmeside" : "Ret " + site.Name, short_name = site is null ? "Hjemmesider" : "Ret siden",
        start_url = site is null ? "/" : "/ret", scope = "/", display = "standalone",
        background_color = "#f4f6f5", theme_color = "#f4f6f5", lang = "da",
        icons = new object[] { new { src = icon, sizes = "any", type = "image/svg+xml", purpose = "any" } },
    }, contentType: "application/manifest+json");
});

// ---- doors ----

app.MapGet("/api/dor", (HttpContext c, string? side, Sites sites, Access access) =>
{
    var site = sites.ByHost(c.Request.Host.Host) ?? (side is null ? null : sites.Get(side));
    return Results.Ok(new
    {
        side = site?.Id, navn = site?.Name, egetDomaene = site is not null && sites.ByHost(c.Request.Host.Host) is not null,
        martin = IsMartin(c, access), ejer = site is not null && Access.IsOwner(site, c.Request.Cookies[OwnerCookie(site.Id)]),
        fuldHjaelp = site?.Package == Package.Fuld,
    });
});

app.MapPost("/api/login", (HttpContext c, LoginBody b, Sites sites, Access access) =>
{
    var site = sites.ByHost(c.Request.Host.Host) ?? (b.Side is null ? null : sites.Get(b.Side));
    switch (access.Login(b.Kode, site, ClientIp(c)))
    {
        case LoginResult.Locked:
            return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);
        case LoginResult.Wrong:
            return Results.Json(new { fejl = site is { Package: Package.Fuld } ? "Martin retter din side for dig – skriv til ham." : "Koden passer ikke." }, statusCode: 401);
        case LoginResult.Martin:
            SetCookie(c, MartinCookie, access.AdminToken);
            return Results.Ok(new { martin = true });
        default:
            SetCookie(c, OwnerCookie(site!.Id), Access.OwnerToken(site));
            return Results.Ok(new { ejer = true });
    }
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    foreach (var k in c.Request.Cookies.Keys.Where(k => k.StartsWith("hj_"))) c.Response.Cookies.Delete(k);
    return Results.Ok();
});

// ---- Martin: the overview and the set-up ----

app.MapGet("/api/sites", (HttpContext c, Sites sites, Editor editor, Access access) =>
    IsMartin(c, access) ? Results.Ok(sites.All().Select(s => Overview(s, sites, editor))) : Results.Unauthorized());

app.MapPost("/api/sites", (HttpContext c, NewSiteBody b, Sites sites, Editor editor, Access access) =>
    !IsMartin(c, access) ? Results.Unauthorized()
        : Try(() => Results.Ok(Overview(sites.Create(b.Navn ?? "", b.Kortnavn, b.Pakke, b.Kode, b.Domaene), sites, editor))));

app.MapPut("/api/sites/{id}/opsaet", (HttpContext c, string id, SetUpBody b, Sites sites, Editor editor, Access access) =>
    !IsMartin(c, access) ? Results.Unauthorized()
        : Try(() => Results.Ok(Overview(sites.SetUp(id, b.Navn ?? "", b.Undertitel ?? "", b.Farve ?? "", b.Telefon ?? "", b.Mail ?? "",
            b.Domaener ?? [], b.Pakke, b.Pris, b.Note ?? "", b.NyKode), sites, editor))));

app.MapDelete("/api/sites/{id}/kode", (HttpContext c, string id, Sites sites, Access access) =>
    !IsMartin(c, access) ? Results.Unauthorized() : Try(() => { sites.RemoveOwnerCode(id); return Results.Ok(); }));

app.MapPost("/api/sites/{id}/logo", async (HttpContext c, string id, IFormFile fil, Sites sites, Access access) =>
{
    if (!IsMartin(c, access)) return Results.Unauthorized();
    return await TryAsync(async () =>
    {
        await using var s = fil.OpenReadStream();
        await sites.SetLogoAsync(id, s, c.RequestAborted);
        return Results.Ok();
    });
}).DisableAntiforgery();

// The hand-over zip: Martin, and the owner too - it is their site.
app.MapGet("/api/sites/{id}/zip", async (HttpContext c, string id, Sites sites, Access access) =>
{
    if (sites.Get(id) is not { } site || !MayEdit(c, access, site)) { c.Response.StatusCode = 403; return; }
    c.Response.ContentType = "application/zip";
    c.Response.Headers.ContentDisposition = $"attachment; filename=\"{site.Id}-{DateTime.Now:yyyy-MM-dd}.zip\"";
    await sites.WriteZipAsync(id, c.Response.Body, c.RequestAborted);
});

// ---- editing: Martin or the owner ----

var edit = app.MapGroup("/api/sites/{id}");
edit.AddEndpointFilter(async (ctx, next) =>
{
    var c = ctx.HttpContext;
    var id = (string)c.Request.RouteValues["id"]!;
    var sites = c.RequestServices.GetRequiredService<Sites>();
    var access = c.RequestServices.GetRequiredService<Access>();
    if (sites.Get(id) is not { } site) return Results.NotFound();
    return MayEdit(c, access, site) ? await next(ctx) : Results.Unauthorized();
});

edit.MapGet("", (HttpContext c, string id, Sites sites, Editor editor, Access access) =>
{
    var s = sites.Get(id)!;
    var martin = IsMartin(c, access);
    return Results.Ok(new
    {
        info = martin ? Overview(s, sites, editor) : null,
        id = s.Id, navn = s.Name, undertitel = s.Tagline, farve = s.Accent, telefon = s.Phone, mail = s.Mail,
        logo = s.Logo is null ? null : $"/s/{s.Id}/billeder/{s.Logo}", pakke = s.Package.ToString(),
        preview = PreviewUrl(s), live = LiveUrl(s), fortryd = editor.UndoSteps(s.Id),
        brugt = sites.UsedBytes(s.Id), plads = SiteRules.Quota(s.Package),
        sider = s.Pages.Select(p => new
        {
            p.Slug, titel = p.Title, iMenu = p.InMenu, forside = p.Slug == SiteRules.FrontSlug,
            bokse = p.Blocks.Select(b => new
            {
                b.Id, billede = b.Kind == BlockKind.Picture, tekst = b.Text,
                url = b.Picture is null ? null : $"/s/{s.Id}/billeder/{b.Picture}",
            }),
        }),
    });
});

edit.MapPost("/sider", (string id, TitleBody b, Editor editor) => Try(() => Results.Ok(new { slug = editor.AddPage(id, b.Titel ?? "").Slug })));

edit.MapPut("/sider/{slug}", (string id, string slug, PageBody b, Editor editor) => Try(() =>
{
    if (b.Titel is not null) editor.RenamePage(id, slug, b.Titel);
    if (b.IMenu is { } show) editor.ShowInMenu(id, slug, show);
    return Results.Ok();
}));

edit.MapPost("/sider/{slug}/flyt", (string id, string slug, MoveBody b, Editor editor) => Try(() => { editor.MovePage(id, slug, b.Retning); return Results.Ok(); }));
edit.MapDelete("/sider/{slug}", (string id, string slug, Editor editor) => Try(() => { editor.RemovePage(id, slug); return Results.Ok(); }));
edit.MapPost("/sider/{slug}/tekst", (string id, string slug, TextBody b, Editor editor) => Try(() => Results.Ok(new { id = editor.AddText(id, slug, b.Tekst ?? "").Id })));

edit.MapPost("/sider/{slug}/billede", async (HttpContext c, string id, string slug, IFormFile fil, Editor editor) =>
    await TryAsync(async () =>
    {
        var form = c.Request.Form;
        await using var s = fil.OpenReadStream();
        var b = await editor.PictureAsync(id, slug, form["boks"].FirstOrDefault(), s, form["tekst"].FirstOrDefault() ?? "", c.RequestAborted);
        return Results.Ok(new { id = b.Id });
    })).DisableAntiforgery();

edit.MapPut("/bokse/{bid}", (string id, string bid, TextBody b, Editor editor) => Try(() => { editor.EditText(id, bid, b.Tekst ?? ""); return Results.Ok(); }));
edit.MapPost("/bokse/{bid}/flyt", (string id, string bid, MoveBody b, Editor editor) => Try(() => { editor.MoveBlock(id, bid, b.Retning); return Results.Ok(); }));
edit.MapDelete("/bokse/{bid}", (string id, string bid, Editor editor) => Try(() => { editor.RemoveBlock(id, bid); return Results.Ok(); }));

edit.MapPost("/fortryd", (string id, Editor editor) =>
    editor.Undo(id) ? Results.Ok() : Results.BadRequest(new { fejl = "Der er ikke mere at fortryde." }));

app.Run();

internal sealed record LoginBody(string? Kode, string? Side);
internal sealed record NewSiteBody(string? Navn, string? Kortnavn, Package Pakke, string? Kode, string? Domaene);
internal sealed record SetUpBody(string? Navn, string? Undertitel, string? Farve, string? Telefon, string? Mail, List<string>? Domaener,
    Package Pakke, int? Pris, string? Note, string? NyKode);
internal sealed record TitleBody(string? Titel);
internal sealed record PageBody(string? Titel, bool? IMenu);
internal sealed record TextBody(string? Tekst);
internal sealed record MoveBody(int Retning);

public partial class Program;
