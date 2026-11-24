using ITMartin.Shared.UI.Kolibri;
using ITMartinLager.Application;
using ITMartinLager.Infrastructure;
using ITMartinLager.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

// Bogshoppen Lager (2026-10-05, user: "100000+ products I need to sell ... all trade concentrated on Bogshoppen.dk").
// Step 1 of the plan: the register. ITMartin is the brand (the CVR is Den Digitale Rådgiver's); Bogshoppen is the shop name.
// Layers: Domain (rules) / Application (use cases + ports) / Infrastructure (SQLite, Claude, price sites) / this Server.
var builder = WebApplication.CreateBuilder(args);
// Every SQL statement at Information drowned real errors in the log (the jobs query every few seconds).
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Lager";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Alt det, vi har – og ret de svære.";
    k.About =
    [
        "Bogshoppen Lager er registret over alt det, Bogshoppen har: bøger, Anders And-blade, Jumbobøger, tegneserier, magasiner, CD'er, DVD'er, Blu-rays, spil og Magic-kort. Formål: find en vare, se den, ret den.",
        "Billederne tages i Vareinfo (genvejen på iPhone eller Upload). De kommer herind af sig selv og læses med det samme: titel, nummer, år og stand. Hver dag er én kasse, fx »15. november« – skriv navnet på kassen, så tingene kan findes igen.",
    ];
    k.HowTo =
    [
        "Ret de svære (øverst på forsiden): de ting, hvor AI'en var usikker – én ad gangen. Se på billedet, ret det, der er forkert, og tryk Gem og næste (eller Spring over). Står der Færdig, er der intet at rette. Priserne sættes i Vurdering.",
        "Billederne læses om aftenen (kl. 21, eller når 100 venter) i én samlet omgang til halv pris – svarene kommer som regel inden for en time. Indtil da står der øverst, hvor mange der venter. Billeder, der skal tages igen (drejet, uskarpt, for langt væk …), står under Tag igen med grunden.",
        "Søg: skriv titel, kunstner, serie, nummer eller år. Nye i dag viser dagens ting, og Vis de interessante viser guld, sølv, bronze og måske. Flere valg har kasse, kategori, sprog og pris. Fjern tager en forkert læst vare ud af listen – den gemmes som Ikke købt, og Fortryd bringer den tilbage. På en vare fører \"Tilbage til listen\" til den samme søgning. Et prismærke på tingen bliver dækket på dens billede (nye billeder).",
        "Kasser: hver dag fra Vareinfo er én kasse. Skriv kassens navn (fx 15. november) i søgefeltet for at se alt fra den.",
        "Mangler en dansk tegneserie sit år, slår Lager serien og nummeret op i Grand Comics Database - året sættes kun på, når det er sikkert.",
        "Ret det, AI'en har læst forkert, på varens side. Står der ⚠ Tjek navn ved en vare, var AI'en usikker – se efter på billedet, ret navnet og tryk Gem. Først da kommer den på bogshoppen.dk.",
        "Priserne findes på DBA, GulogGratis, Accord (brugte cd'er og plader), Nemos Bibliotek, Faraos og AbeBooks (antikvariater – kun de 2 mest realistiske vises). Nypriser: Bog & idé (bøger) og Fantask (tegneserier) - en brugt bliver aldrig dyrere. Discogs viser hvilken plade det er, og hvor mange samlere der ønsker den, men tæller ikke med i prisen.",
        "DBA og GulogGratis søges kun i den kategori, tingen hører til: blade og bøger under Bøger og Samlerobjekter, film og musik under Musik og film, spil under spil. Så finder »E-Man« ikke længere lastbiler, og »Vox« ikke forstærkere. Et navn med bindestreg (X-Men, E-Man, X-Force) skal stå helt i annoncen, og korte ord skal være hele ord – »man« tæller ikke i »Manhattan«.",
        "Ret de svære: de værste først. Under »De værste« står dem, AI'en næsten ikke kunne læse (under 25 %) – »Kassér alle« fjerner dem på én gang (gemt som Ikke købt, »Fortryd« henter dem tilbage). »Tag igen«: »Fjern« tager et billede af listen; selve billedet bliver på NAS'en.",
        "Billeder i butikken: bogshoppen.dk viser kun rigtige udsnit – ikke hele bunken, ikke smalle striber (fx kun ryggen af en cd) og ikke små billeder. Tingen er stadig til salg (»billede kommer«). I Priser står der hvorfor, og »Ret udsnittet« eller et nyt billede retter det.",
        "Priser sættes i Priser (Vurdering) – derfor er der ikke længere et Pris-filter i Lager.",
        "Sådan findes prisen (prisanalysen 16. november): annoncer (DBA og GulogGratis) vises kun som links – de er det, sælgerne håber på, ofte 2-5 gange mere end det, ting faktisk sælges for. Cd'er og plader: Discogs. Tegneserier og blade: Faraos og Nemos. Film: standardprisen. Ny og Som ny bøger: 25 % under Bog & idé, med Bog & idés link og pris som bevis. Når en slags har 50 salg, retter forslaget sig efter det, der faktisk blev solgt for.",
        "Find en vare: skriv titel, serie, år – eller varens nr. (står under overskriften på varen, fx 3372). Er det et varenummer, kommer den vare øverst som en knap.",
        "Annoncer med flere ting tæller ikke: en liste af titler (»Et spind af løgne, Rødt vand, Det sidste barn og Jernhuset«), »m.m.«, sæt, trilogi, blandet, bunke – og heller ikke købes-/søges-annoncer.",
        "Prislinks viser kun annoncer for netop den ting alene: ikke plakater, kasketter, samlekort, puslespil, flere stk. i én annonce, tingen sammen med noget andet (+, inkl., og ...), en liste af titler (\"Rød hals, Politi, Genfærd ...\") eller tv-serien af samme navn. En dvd-boks (Matador Kollektionen) sammenlignes med andre bokse. Anders And-blade søges som serie, nummer og år – ikke overskriften på forsiden. Tryk på en pris, så åbner selve annoncen – ikke bare butikkens forside.",
        "Er netop dette nummer ikke til salg nogen steder, viser Lager hvad andre numre af samme serie koster (fx andre Basserne på Nemos) – som pejlemærke, ikke som pris, med en linje om hvad de koster på hver side (danske først). Var AI'en usikker på en vare, men en annonce har samme serie og nummer, får den alligevel sin pris.",
        "Kategorier på Søg: Bøger, Tegneserier (album som Lucky Luke), Serieblade (Anders And, Basserne, Jonah Hex, Kong Kylie), Gamle blade (alle blade fra før 1990), Jumbobøger, Superhelte (Project X, Edderkoppen), Film, Musik, Computerspil, Brætspil og kortspil, Magic og Andet. Vælg også Dansk eller Engelsk. Kategori og sprog sættes af sig selv ud fra varens slags, serie og år – sproget læses med billedet, ældre varer får et gæt ud fra titlen.",
        "Ansvarlig: Rico har Tegneserier, Superhelte og Film; Martin resten.",
        "Genrer: musik, film og bøger får af sig selv en genre (fx Dansktop, Punk, Krimi, Børnefilm) fra gratis kilder – MusicBrainz, TMDb og Open Library. Tegneserier, blade og spil får genren ud fra serien og titlen (fx Jonah Hex = Western, Basserne = Humor, Anders And = Disney). Hver ting slås op én gang, uden AI. Kunderne vælger genren i butikken på bogshoppen.dk.",
        "Standardpris, når der ikke er fundet en pris (sikker læsning, søgt): cd 15 kr, dvd 20, vinyl 20, vhs 10, blu-ray 25, bog 25, tegneserie 25, superhelteblad 40, jumbobog 20 – andre slags får kategoriens pris. Anders And-blade er 3 kr stk. Er AI'en usikker, venter varen på en person.",
        "💎 Sjælden – vurderes manuelt: sæt hak på en vare (fx et gammelt Anders And-blad). Den kommer ud af 3 kr-kassen, får ingen automatisk pris og venter i Vurdering under Skal vurderes, hvor du skriver en kommentar, sætter prisen eller sender den på auktion.",
        "Guld, sølv og bronze og Ny i kiosken er flyttet til Vurdering. Bøger, Dagens tilbud, Strik, Ting og Magic står under »Værktøjer, der flytter snart« nederst på forsiden, indtil de flytter til Vurdering, Salg og deres egne apps.",
    ];
    k.Version = "2026.11.16";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinLager.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddLagerInfrastructure(builder.Configuration);
builder.Services.AddLagerApplication();
builder.Services.AddHostedService<PriceHunterJob>();
builder.Services.AddHostedService<AuctionSyncJob>();
builder.Services.AddHostedService<PhotoCheckJob>();
builder.Services.AddHostedService<GoldListJob>();
builder.Services.AddHostedService<IntakeJob>();
builder.Services.AddHostedService<KolofonJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-lager");

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

var app = builder.Build();
app.Services.UpgradeLagerDatabase();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One shared PIN for Martin and Rico (Lager__Pin in magic.env). Everything but login and static files needs it; Salg
// calls /api/salg/* with its key instead.
var pin = app.Configuration["Lager:Pin"] ?? "";
var salgKey = app.Configuration["Lager:SalgKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["lager_auth"] == pin;
// A helper's kundenummer (Kunder's Hjælper tick, user 2026-10-24) - 8 digits, never the PIN.
async Task<string?> HelperAsync(HttpContext ctx, string? typed)
{
    var digits = new string((typed ?? "").Where(char.IsDigit).ToArray());
    return digits.Length == 8 && digits != pin ? await ctx.RequestServices.GetRequiredService<IHelperNumbers>().HelperNameAsync(digits) : null;
}
// An admin's kundenummer (Kunder's Admin tick, 2026-10-26) opens everything, like the PIN.
async Task<string?> AdminAsync(HttpContext ctx, string? typed)
{
    var digits = new string((typed ?? "").Where(char.IsDigit).ToArray());
    return digits.Length == 8 && digits != pin ? await ctx.RequestServices.GetRequiredService<IHelperNumbers>().AdminNameAsync(digits) : null;
}
bool FromSalg(HttpContext ctx) => salgKey.Length >= 12 && ctx.Request.Headers[StockHeaders.Key] == salgKey;
// Vurdering (step 2, 2026-11-05) has its own key - it may only read the things to price and set a price.
var vurderingKey = app.Configuration["Lager:VurderingKey"] ?? "";
bool FromVurdering(HttpContext ctx) => vurderingKey.Length >= 12 && ctx.Request.Headers[VurderingHeaders.Key] == vurderingKey;
// Vareinfo's Tjek page (2026-11-10) asks with the key Lager uses towards Vareinfo (Lager__InfoKey = Vareinfo__LagerKey).
var infoKey = app.Configuration["Lager:InfoKey"] ?? "";
bool FromVareinfo(HttpContext ctx) => infoKey.Length >= 12 && ctx.Request.Headers[CaptureHeaders.Key] == infoKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    // ITMartin Admin asks with its own key - the endpoint checks it.
    if (p == ITMartinAdmin.Contracts.AdminStatusApi.Path) { await next(); return; }
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/salg/")) { if (FromSalg(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (p.StartsWith("/api/vurdering/")) { if (FromVurdering(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (p.StartsWith("/api/vareinfo/")) { if (FromVareinfo(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (LoggedIn(ctx)) { ctx.Items[nameof(LagerUser)] = new LagerUser("Martin", true); await next(); return; }
    if (open) { await next(); return; }
    if (await AdminAsync(ctx, ctx.Request.Cookies["lager_auth"]) is { } admin)
    {
        ctx.Items[nameof(LagerUser)] = new LagerUser(admin, true);
        await next();
        return;
    }
    if (await HelperAsync(ctx, ctx.Request.Cookies["lager_auth"]) is { } helper)
    {
        if (!LagerUser.HelperMayOpen(p)) { ctx.Response.Redirect("/"); return; }
        ctx.Items[nameof(LagerUser)] = new LagerUser(helper, false);
        await next();
        return;
    }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", async (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    var isAdmin = pin.Length >= 6 && typed == pin || await AdminAsync(ctx, typed) is not null;
    var value = pin.Length >= 6 && typed == pin ? pin
        : isAdmin || await HelperAsync(ctx, typed) is not null ? new string(typed.Where(char.IsDigit).ToArray()) : null;
    if (value is null) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("lager_auth", value, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

// ITMartin Admin's front page (2026-10-26): what waits to be read. Only with Admin__StatusKey.
app.MapGet(ITMartinAdmin.Contracts.AdminStatusApi.Path, async (HttpContext ctx, IntakeUseCases intake, BoxUseCases boxes) =>
{
    if (!ITMartinAdmin.Contracts.AdminStatusApi.KeyOk(app.Configuration["Admin:StatusKey"], ctx.Request.Headers[ITMartinAdmin.Contracts.AdminStatusApi.KeyHeader])) return Results.Unauthorized();
    var waiting = await intake.BoxesAsync();
    return Results.Ok(new ITMartinAdmin.Contracts.AppStatus("Lager",
    [
        new("billeder venter på at blive læst", waiting.Sum(b => b.Waiting), true, "https://lager.bogshoppen.dk/kasser"),
        new("stregkoder ukendt – tag et billede af forsiden", waiting.Sum(b => b.Unknown), true, "https://lager.bogshoppen.dk/kasser"),
        new("kasser", (await boxes.AllAsync()).Count, false, "https://lager.bogshoppen.dk/kasser"),
    ]));
});

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("lager_auth"); return Results.Redirect("/login"); });

// Photos: the item's own photo and its kolofon are here; the box photos are in Vareinfo (old links are sent on).
static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/varefoto/{id:int}", async (int id, int? n, ItemUseCases items) => Jpeg(await items.PhotoFileAsync(id, n ?? 1)));
app.MapGet("/foto/{id:int}", (int id, VareinfoLinks v) => Results.Redirect(v.Photo(id)));
app.MapGet("/kolofonfoto/{id:int}", async (int id, KolofonUseCases k) => Jpeg(await k.PhotoFileAsync(id)));

app.MapStockApi();
app.MapVurderingApi();   // Vurdering (step 2)
app.MapTjekApi();        // Vareinfo's Tjek answers (2026-11-10)

// Fixed price per kind on everything for sale (user 2026-11-24) - behind the Lager PIN like every /api route.
app.MapPost("/api/priser/fast", async (PriceHuntUseCases hunt, CancellationToken ct) =>
{
    var (changed, own, all) = await hunt.ApplyFixedPricesAsync(ct);
    return Results.Ok(new { kontrolleret = all, aendret = changed, skalVurderes = own });
}).DisableAntiforgery();

// Change the type of several things at once (2026-11-24) - behind the Lager PIN like every /api route.
app.MapPost("/api/varer/type", async (KindChange body, ItemUseCases items, CancellationToken ct) =>
    Results.Ok(new { aendret = await items.SetKindAsync(body.Ids ?? [], body.Kind ?? "", ct) })).DisableAntiforgery();

app.MapGet("/api/eksport.csv", async (ExportUseCases export) =>
    Results.File(await export.ItemsCsvAsync(), "text/csv", $"bogshoppen-lager-{DateTime.Now:yyyy-MM-dd}.csv"));
// Item photos as a zip: all, or one box (?kasse=K0001). Built in a temp file that deletes itself when sent - all photos
// can be more than should sit in memory.
app.MapGet("/api/varefotos.zip", async (string? kasse, ExportUseCases export, CancellationToken ct) =>
{
    var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
    if (await export.PhotosZipAsync(file, kasse, ct) == 0)
    {
        await file.DisposeAsync();
        return Results.Text("Der er ingen varefotos" + (kasse is null ? "" : $" i {kasse}") +
            " endnu. Åbn en vare og tryk 'Tag billede af varen' – så kommer billedet med her.", "text/plain; charset=utf-8");
    }
    file.Position = 0;
    return Results.File(file, "application/zip", $"varefotos-{kasse ?? "alle"}-{DateTime.Now:yyyy-MM-dd}.zip");
});
app.MapGet("/api/boeger.csv", async (ExportUseCases export) =>
    Results.File(await export.BooksCsvAsync(), "text/csv", $"boeger-tjekket-{DateTime.Now:yyyy-MM-dd}.csv"));

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

public sealed record KindChange(List<int>? Ids, string? Kind);
