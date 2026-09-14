using ITMartinElPriser.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Singletons with their own HttpClient: both services cache (prices for
// 30 min, the Eloverblik access token for 12 h) - a transient-per-request
// registration would throw the cache away every call.
builder.Services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
builder.Services.AddSingleton<HouseholdStore>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton<RunLogService>();
builder.Services.AddHostedService<NotificationScheduler>();
builder.Services.AddSingleton(sp => new EloverblikService(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, sp.GetRequiredService<ILogger<EloverblikService>>()));
builder.Services.AddSingleton<ConsumptionStore>();
builder.Services.AddSingleton<ConsumptionSync>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConsumptionSync>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

// ── Push subscription API (called from wwwroot/js/push.js) ──────────────────

app.MapGet("/api/push/public-key", (PushService push) => Results.Text(push.PublicKey));

app.MapPost("/api/push/subscribe", (SubscribeRequest req, HouseholdStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Endpoint)) return Results.BadRequest();
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
    });
    return Results.Ok(new { id = sub!.Id });
}).DisableAntiforgery();

app.MapPost("/api/push/unsubscribe", (UnsubscribeRequest req, HouseholdStore store) =>
{
    store.Update(d => d.Subscribers.RemoveAll(s => s.Endpoint == req.Endpoint));
    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/api/push/status", (string endpoint, HouseholdStore store) =>
{
    var s = store.Get().Subscribers.FirstOrDefault(x => x.Endpoint == endpoint);
    return s is null ? Results.NotFound() : Results.Ok(new { s.Name, s.NotifyCheapest, s.NotifyExpensive });
});

// "Send mig en test" - proves the whole chain on this phone right now.
app.MapPost("/api/push/test", async (UnsubscribeRequest req, HouseholdStore store, PushService push, ElectricityPriceService prices) =>
{
    var sub = store.Get().Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
    if (sub is null) return Results.NotFound();
    var data = store.Get();
    var snap = PriceModel.Build(await prices.GetPricesAsync(data.Settings.PriceArea), data, DateTime.Now);
    var body = snap.NowKrPerKwh is { } p ? $"Lige nu koster strømmen {p:0.00} kr/kWh. Beskeder virker ✓" : "Beskeder virker ✓";
    var ok = await push.SendAsync(sub, new PushService.Message("ElPriser", body));
    return ok ? Results.Ok() : Results.StatusCode(410);
}).DisableAntiforgery();

// Plain JSON for anyone who wants the numbers (or for tests).
app.MapGet("/api/snapshot", async (ElectricityPriceService prices, HouseholdStore store) =>
{
    var data = store.Get();
    return Results.Ok(PriceModel.Build(await prices.GetPricesAsync(data.Settings.PriceArea), data, DateTime.Now));
});

app.MapRazorComponents<ITMartinElPriser.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name, bool NotifyCheapest, bool NotifyExpensive);
public sealed record UnsubscribeRequest(string Endpoint);
