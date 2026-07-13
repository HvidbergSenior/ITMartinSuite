using ITMartinElPriser.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpClient<ElectricityPriceService>();
builder.Services.AddSingleton<PriceHistoryStore>();
builder.Services.AddSingleton<RunLogStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// ── Prices ───────────────────────────────────────────────────────────────────

app.MapGet("/api/prices", async (ElectricityPriceService svc, string area = "DK1") =>
    Results.Ok(await svc.GetPricesAsync(area)));

app.MapGet("/api/cheapest-window", async (ElectricityPriceService svc, int hours, string area = "DK1") =>
{
    var prices = await svc.GetPricesAsync(area);
    var window = svc.FindCheapestWindow(prices, hours);
    return window is null ? Results.NotFound() : Results.Ok(window);
});

app.MapGet("/api/weekly-pattern", (PriceHistoryStore history, string area = "DK1") =>
    Results.Ok(new { Days = history.DaysOfHistory(area), Pattern = history.GetWeeklyPattern(area) }));

// ── Run log (device start tracking + cost) ──────────────────────────────────

app.MapPost("/api/runs", async (ElectricityPriceService priceSvc, RunLogStore runs, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<LogRunBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Device) || body.EstKwh <= 0)
        return Results.BadRequest();

    var prices = await priceSvc.GetPricesAsync(body.Area ?? "DK1");
    var now = prices
        .Where(p => p.TimeDk <= DateTime.Now)
        .OrderByDescending(p => p.TimeDk)
        .FirstOrDefault();

    var currentPrice = now?.PriceKrPerKwh ?? 0;
    var run = runs.Add(body.Device, body.EstKwh, currentPrice);
    return Results.Ok(run);
});

app.MapGet("/api/runs", (RunLogStore runs) =>
{
    var (runsWeek, costWeek, runsMonth, costMonth) = runs.GetTotals();
    return Results.Ok(new
    {
        Recent = runs.GetRecent(30),
        RunsThisWeek = runsWeek,
        CostThisWeekKr = costWeek,
        RunsThisMonth = runsMonth,
        CostThisMonthKr = costMonth,
    });
});

// ── Blazor (static SSR only - no interactive render mode anywhere) ──────────

app.MapRazorComponents<ITMartinElPriser.Server.App>();

app.Run();

record LogRunBody(string Device, double EstKwh, string? Area);
