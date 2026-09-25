using ITMartin.Shared.UI.Kolibri;
using ITMartinElPriser.Core;
using ITMartinElPriser.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "ElPriser";
    k.KolibriName = "Kolibri Puls";
    k.Family = "puls";
    k.Tagline = "Hvad koster strømmen lige nu – og hvornår er den billig?";
    k.About =
    [
        "ElPriser viser timeprisen på strøm i dag og i morgen, så du kan lægge vask, opvask og opladning på de billige timer.",
        "Regning forklarer din elregning linje for linje, og Besked sender et prik til telefonen, når prisen falder.",
        "Vil du have din egen elmåler og dine apparater med, er MinElpris den store søster.",
    ];
    k.HowTo =
    [
        "Åbn appen fra ikonet på telefonen – forsiden viser prisen nu.",
        "Slå Besked til for at få et prik, når strømmen er billig.",
        "Tryk Regning, hvis du vil forstå din elregning.",
    ];
    k.Version = "2026.09";
    k.IconPath = "icon.svg";
    k.ThemeColor = "#0b1220";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
// Singleton with its own HttpClient: it caches prices for 30 min - a
// transient-per-request registration would throw the cache away every call.
builder.Services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
builder.Services.AddSingleton(sp => new Co2Service(new HttpClient(), sp.GetRequiredService<ILogger<Co2Service>>()));
builder.Services.AddSingleton(sp => new SupplierCatalog(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, sp.GetRequiredService<ILogger<SupplierCatalog>>()));
builder.Services.AddSingleton(sp => new GridTariffs(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, sp.GetRequiredService<ILogger<GridTariffs>>()));
builder.Services.AddSingleton<SubscriberStore>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddScoped<PrefsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

// ── Push subscription API (called from wwwroot/js/push.js) ──────────────────

app.MapGet("/api/push/public-key", (PushService push) => Results.Text(push.PublicKey));

app.MapPost("/api/push/subscribe", (SubscribeRequest req, HttpContext http, SubscriberStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Endpoint)) return Results.BadRequest();
    var settings = PrefsService.ReadCookie(http);
    PushSubscriber? sub = null;
    store.Update(d =>
    {
        sub = d.Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
        if (sub is null)
        {
            sub = new PushSubscriber { Endpoint = req.Endpoint };
            d.Subscribers.Add(sub);
        }
        sub.P256dh = req.P256dh;
        sub.Auth = req.Auth;
        sub.Name = string.IsNullOrWhiteSpace(req.Name) ? sub.Name : req.Name.Trim();
        sub.NotifyCheapest = req.NotifyCheapest;
        sub.NotifyExpensive = req.NotifyExpensive;
        sub.Settings = settings;
    });
    return Results.Ok(new { id = sub!.Id });
}).DisableAntiforgery();

app.MapPost("/api/push/unsubscribe", (UnsubscribeRequest req, SubscriberStore store) =>
{
    store.Update(d => d.Subscribers.RemoveAll(s => s.Endpoint == req.Endpoint));
    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/api/push/status", (string endpoint, SubscriberStore store) =>
{
    var s = store.Get().Subscribers.FirstOrDefault(x => x.Endpoint == endpoint);
    return s is null ? Results.NotFound() : Results.Ok(new { s.Name, s.NotifyCheapest, s.NotifyExpensive });
});

// "Send mig en test" - proves the whole chain on this phone right now.
app.MapPost("/api/push/test", async (UnsubscribeRequest req, SubscriberStore store, PushService push, ElectricityPriceService prices) =>
{
    var sub = store.Get().Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
    if (sub is null) return Results.NotFound();
    var snap = PriceModel.Build(await prices.GetPricesAsync(sub.Settings.PriceArea), sub.Settings, [], DkTime.Now);
    var body = snap.NowKrPerKwh is { } p ? $"Lige nu koster strømmen {p:0.00} kr/kWh. Beskeder virker ✓" : "Beskeder virker ✓";
    var ok = await push.SendAsync(sub, new PushService.Message("ElPriser", body));
    return ok ? Results.Ok() : Results.StatusCode(410);
}).DisableAntiforgery();

// Plain JSON for anyone who wants the numbers: ?area=DK2&allIn=false
app.MapGet("/api/snapshot", async (ElectricityPriceService prices, HttpContext http, string? area, bool? allIn) =>
{
    var s = PrefsService.ReadCookie(http);
    if (area is "DK1" or "DK2") s.PriceArea = area;
    if (allIn is { } a) s.ShowAllIn = a;
    return Results.Ok(PriceModel.Build(await prices.GetPricesAsync(s.PriceArea), s, Appliance.Defaults(), DkTime.Now));
});

app.MapKolibri();

app.MapRazorComponents<ITMartinElPriser.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name, bool NotifyCheapest, bool NotifyExpensive);
public sealed record UnsubscribeRequest(string Endpoint);
