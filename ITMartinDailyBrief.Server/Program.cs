using ITMartin.Shared.UI.Kolibri;
using ITMartinDailyBrief.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "DailyBrief";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Dine kilder, hver morgen, kogt ned til det vigtigste – læst eller læst højt.";
    k.About =
    [
        "DailyBrief samler det nyeste fra de kilder, du selv vælger, ét sted – i din rækkefølge.",
        "Læs det på skærmen, eller lad det blive læst højt, mens du cykler.",
    ];
    k.HowTo =
    [
        "Under Indstillinger vælger du kilderne.",
        "Åbn appen om morgenen – forsiden er dagens brief.",
        "Tryk Lyd for at få det læst højt.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#0F1117";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("feed", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("DailyBrief/1.0 (+https://itmartin.dk)");
});

builder.Services.AddSingleton<FeedService>();
builder.Services.AddSingleton<BriefingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapKolibri();

app.MapRazorComponents<ITMartinDailyBrief.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
