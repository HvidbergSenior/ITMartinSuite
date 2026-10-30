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
        "Vi er en familie med tre unge på 20+, en mor og en far. Hver hjælper er født af noget i hverdagen, der bare ikke var rigtigt. De er et supplement til vores egen proces – ingen tvang. Vi prøver ikke at være frelste; vi deler bare, hvad der virkede for os. Siden er ikke politisk.",
        ["FileSorter", "ElPriser", "Budget", "Bogshoppen"],
        [
            new("FileSorter – billederne fundet igen",
            [
                "87.009 filer blev til 34.079 rigtige billeder og videoer, sorteret efter år. Resten var dubletter og skrammel – som ellers ville ligge på en server og bruge strøm.",
            ], "Telefonen bliver ved med at minde om, at lageret er fuldt – så det nemmeste er at sige ja til mere plads i skyen i stedet for at rydde op."),
            new("ElPriser – fra mors påmindelser til én knap",
            [
                "Nu trykker alle på én knap ved maskinen og ser, hvad det koster – for hele turen, ikke kun lige nu. Start nu eller vent. Mor behøver ikke minde os om det.",
            ], "Mor var den eneste, der holdt øje med strømprisen – og elselskabets app var fyldt med alt muligt andet."),
            new("Budget – indsigt i stedet for regler",
            [
                "Flere penge fra de unge ændrer ingen vaner. Budget viste os abonnementerne – og hvad parkering koster i forhold til cyklen. Bare at forstå, hvad tingene koster, er en besparelse.",
            ], "Indtægten faldt, og netbanken viste ikke, hvor pengene faktisk gik hen."),
            new("Bogshoppen – det er dig, der bestemmer",
            [
                "En DVD virker på en PS3 – det er dig, der bestemmer, ikke streamingtjenesten. Fem film på fredag, og familien vælger én, alle kan lide – også mor.",
            ], "100.000 bøger, film og CD'er med værdi var på vej i skraldespanden."),
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
