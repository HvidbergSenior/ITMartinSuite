using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinKanal.Application;
using ITMartinKanal.Domain;
using ITMartinKanal.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Kanal (2026-10-30, "just for demo purposes"): an open comment channel on Martin's photoserver, asleep at night - the first
// one for Pelle Dragsted on data centres and green energy. Built on ElPriser as the template.
builder.Services.AddKolibri(k =>
{
    k.Name = "Kanal";
    k.KolibriName = "Kolibri Puls";
    k.Family = "puls";
    k.Tagline = "Skriv din kommentar – den kører på en gammel computer.";
    k.About =
    [
        "Kanal er en lille side, hvor alle kan skrive en kommentar om et emne. Den er lavet som et eksempel på, hvor lidt der skal til.",
        "Siden kører på en gammel computer hjemme hos Martin – ikke i et stort datacenter. Den sover om natten fra kl. 23 til kl. 7 og bruger kun strøm, når den er tændt.",
        "Du behøver ikke skrive dit navn. Telefonnumre og e-mails bliver fjernet automatisk. Ingen konto, ingen reklamer, ingen sporing.",
    ];
    k.HowTo =
    [
        "Vælg et emne, skriv din kommentar og tryk Send. Dit navn kan undværes.",
        "Kommentarerne står nyeste først. Er siden lukket, er det nat – kom igen efter kl. 7.",
    ];
    k.Version = "2026.10.30";
    k.ThemeColor = "#0b1220";
});

builder.Services.AddRazorComponents();
builder.Services.AddKanalInfrastructure(builder.Configuration["DataDir"] ?? "/data");
// Nothing configured = the demo channel. Independent on purpose (user 2026-10-30: "the discussion about datacentre and ai should
// be independent not political and/or party related") - a neutral address and text, not "Pelle's page", though the link goes to him.
var channels = builder.Configuration.GetSection("Kanal:Channels").GetChildren()
    .Select(c => new KanalChannel((c["Slug"] ?? "").ToLowerInvariant(), c["Title"] ?? "", c["Intro"] ?? "",
        c.GetSection("Topics").GetChildren().Select(t => t.Value ?? "").Where(t => t.Length > 0).ToArray())).ToList();
if (channels.Count == 0)
    // Re-focused 2026-10-30 ("it should not be Pelle. Rather... how digital helpers helped a family with perspective"):
    // Martin's own family, in his own words (shortened, nothing added).
    channels.Add(new KanalChannel("familien", "Sådan gav digitale hjælpere vores familie perspektiv",
        "Vi er en familie med tre unge på 20+, en mor og en far. Her fortæller vi, hvordan fire små digitale hjælpere har ændret den måde, vi ser på vores forbrug – uden tvang, og uden at nogen skal være politiet. Siden er ikke politisk og ikke knyttet til noget parti.",
        ["ElPriser", "Budget", "Bogshoppen", "FileSorter"],
        [
            new("ElPriser – fra mors påmindelser til én knap",
            [
                "Før: Mor fandt de billige timer i elselskabets egen app – men den var fyldt med alt muligt, der ikke var relevant lige der. Så mindede hun os om, at det var billigt at starte vaskemaskinen kl. 13 i dag. Vi andre ville gerne gøre hende glad og prøvede – men nogle gange glemte vi det, og andre gange startede vi den senere. Det gav diskussioner, og mor tænkte: Hvorfor er det kun mig, der tænker på det her?",
                "Nu: Lige når du står ved maskinen, trykker du på en knap på telefonen og ser, hvad det koster nu, hvad det vil koste senere, hvornår det er billigere – og hvor meget du faktisk sparer. Så kan du starte den eller vente.",
                "Mange tror, de sparer, fordi strømmen er billig lige nu, når de trykker start. Men maskinen kører i flere timer, og man betaler prisen i hver af de timer – ikke kun prisen i det øjeblik, man starter den. ElPriser regner hele turen med.",
                "Beskederne fortæller også, når det er billigst lige nu – og om det kan betale sig at vente til i aften. Mor behøver ikke længere minde os om det.",
            ]),
            new("Budget – indsigt i stedet for regler",
            [
                "Før: Vores indtægt faldt drastisk. Børnene skulle selv betale for noget. Men hvad? Skulle de bare betale 1.000 kr om måneden hver? Nej. Bare at smide flere penge ind fra de unge ændrer jo ikke noget ved vanerne. Vi skulle finde ud af, HVOR vi kan ændre noget – og først ville vi have indsigt i, hvad vi faktisk bruger penge på.",
                "Budget viste os, at netbankernes apps ikke er gode nok: MobilePay-overførsler og kategorier gemmer sig. Vi fandt ud af, hvor meget vi bruger på abonnementer – og hvor meget vi bruger på parkering i stedet for at cykle, fordi vi fik det præcise beløb at sammenligne med cyklen.",
                "Bare det at dele forståelsen af, hvad tingene koster, giver i sig selv en besparelse.",
            ]),
            new("Bogshoppen – det er dig, der bestemmer",
            [
                "100.000 bøger, film og CD'er kunne være smidt ud. Men de har værdi. Det handler om bæredygtighed: at give tingene et nyt liv i stedet for at smide dem ud.",
                "Konkurrenterne er Netflix og de andre streamingtjenester. Kan man konkurrere? Ja. En DVD kan afspilles på en PS3, en Blu-ray også. Det er ikke Netflix, der bestemmer, hvad du kan se – det er dig. Ingen censur, ingen manipulation, ingen månedlig betaling.",
                "Og kan du huske dengang i videobutikken? Lej eller køb fem film og se dem sammen fredag aften – i stedet for at scrolle gennem film uden at vælge én. Så kan familien vælge en film, alle kan lide – også mor. Og nogle film er værd at se mere end én gang – de står jo på hylden.",
            ]),
            new("FileSorter – billederne fundet igen",
            [
                "Vores billeder lå spredt over gamle harddiske og telefoner – 87.009 filer. Da de var sorteret, var der 34.079 rigtige billeder og videoer tilbage. Resten var dubletter, skærmbilleder og skrammel.",
                "Nu ligger de sorteret efter år, og vi kan finde dem igen.",
                "Billeder, videoer og film i skyen fylder mere og mere, og de fleste af os sletter aldrig noget. Hver dublet ligger på en server et sted og bruger strøm. Derfor rydder vi op – så serverne ikke bliver fyldt med det, ingen ser.",
            ]),
        ]));
builder.Services.AddKanalApplication(channels, () => DateTime.UtcNow);

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
// Routing after the static files: the page's "/{Slug}" would otherwise catch app.css and icon.svg.
app.UseRouting();
app.UseAntiforgery();

// Martin deletes with his PIN (Kanal__Pin in magic.env); the cookie holds a hash, never the PIN.
var pin = app.Configuration["Kanal:Pin"] ?? "";
string? ownerToken = pin.Length >= 4 ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("kanal|" + pin)))[..32] : null;
bool IsOwner(HttpContext ctx) => ownerToken is not null && ctx.Request.Cookies.TryGetValue("kanal_auth", out var v) && v == ownerToken;

app.MapGet("/", (KanalChannels all) => all.All.Count > 0 ? Results.Redirect("/" + all.All[0].Slug) : Results.NotFound());

app.MapPost("/{slug}/skriv", async (string slug, HttpContext ctx, KanalUseCases kanal) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (form["website"].ToString().Length > 0) return Results.Redirect($"/{slug}?ok=1");   // a bot filled the hidden field
    var error = kanal.Write(slug, form["topic"], form["name"], form["text"]);
    return Results.Redirect($"/{slug}" + (error is null ? "?ok=1#kommentarer" : "?fejl=" + Uri.EscapeDataString(error) + "#skriv"));
}).DisableAntiforgery();

app.MapPost("/{slug}/slet/{id:guid}", (string slug, Guid id, HttpContext ctx, KanalUseCases kanal) =>
{
    if (!IsOwner(ctx)) return Results.NotFound();
    kanal.Delete(id);
    return Results.Redirect($"/{slug}#kommentarer");
}).DisableAntiforgery();

app.MapPost("/ejer", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (ownerToken is null || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("kanal|" + form["pin"].ToString().Trim())))[..32]),
            Encoding.UTF8.GetBytes(ownerToken)))
    {
        await Task.Delay(800);
        return Results.Redirect("/");
    }
    ctx.Response.Cookies.Append("kanal_auth", ownerToken, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapKolibri();
app.MapRazorComponents<ITMartinKanal.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);

app.Run();

public partial class Program
{
    /// <summary>For the page: is this visitor Martin (shows Slet)?</summary>
    public static bool Owner(HttpContext ctx, IConfiguration cfg)
    {
        var pin = cfg["Kanal:Pin"] ?? "";
        if (pin.Length < 4) return false;
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("kanal|" + pin)))[..32];
        return ctx.Request.Cookies.TryGetValue("kanal_auth", out var v) && v == token;
    }
}
