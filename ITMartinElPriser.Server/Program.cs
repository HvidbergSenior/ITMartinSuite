using ITMartin.Ai;
using ITMartin.Ai.Interfaces;
using ITMartinElPriser.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpClient<ElectricityPriceService>();
builder.Services.AddSingleton<PriceHistoryStore>();
builder.Services.AddSingleton<RunLogStore>();
builder.Services.AddSingleton<PriceSettingsStore>();
builder.Services.AddAi();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// ── Settings (grid company + supplier presets, first-run onboarding) ───────

app.MapGet("/api/settings/presets", () => Results.Ok(new
{
    GridCompanies = GridCompanyPreset.All,
    Suppliers = SupplierPreset.All,
}));

app.MapGet("/api/settings", (PriceSettingsStore store) => Results.Ok(store.Get()));

app.MapPost("/api/settings", async (PriceSettingsStore store, HttpContext ctx) =>
{
    var settings = await ctx.Request.ReadFromJsonAsync<PriceSettings>();
    if (settings is null) return Results.BadRequest();

    store.Save(settings);
    return Results.Ok(settings);
});

// Bill scan: photo of an elregning in, best-guess settings out. The user still
// confirms/adjusts before it's saved - this is a starting point, not an
// auto-apply, since a misread number would silently skew every price shown.
app.MapPost("/api/bill-scan", async (IElBillExtractionService ai, HttpRequest req) =>
{
    if (!req.HasFormContentType) return Results.BadRequest();

    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("bill");
    if (file is null || file.Length == 0) return Results.BadRequest();

    var tempPath = Path.Combine(Path.GetTempPath(), $"elbill-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");

    try
    {
        await using (var stream = File.Create(tempPath))
            await file.CopyToAsync(stream);

        var extracted = await ai.ExtractFromImageAsync(tempPath);
        return Results.Ok(extracted);
    }
    finally
    {
        if (File.Exists(tempPath)) File.Delete(tempPath);
    }
});

// ── Prices ───────────────────────────────────────────────────────────────────

app.MapGet("/api/prices", async (ElectricityPriceService svc, PriceSettingsStore settingsStore, string area = "DK1") =>
{
    var settings = settingsStore.Get();
    var prices = await svc.GetPricesAsync(area);
    return Results.Ok(prices.Select(p => PriceBreakdownCalculator.Compute(p, settings)));
});

app.MapGet("/api/cheapest-window", async (ElectricityPriceService svc, PriceSettingsStore settingsStore, int hours, string area = "DK1") =>
{
    var settings = settingsStore.Get();
    var prices = await svc.GetPricesAsync(area);
    var priced = prices.Select(p => PriceBreakdownCalculator.Compute(p, settings)).ToList();
    var window = PriceBreakdownCalculator.FindCheapestWindow(priced, hours);
    return window is null ? Results.NotFound() : Results.Ok(window);
});

app.MapGet("/api/weekly-pattern", (PriceHistoryStore history, string area = "DK1") =>
    Results.Ok(new { Days = history.DaysOfHistory(area), Pattern = history.GetWeeklyPattern(area) }));

// Estimated yearly cost under each supplier preset, holding the grid company
// and usage estimate fixed - lets you see whether switching supplier would
// actually save money, without having to touch your real settings first.
app.MapGet("/api/supplier-comparison", async (ElectricityPriceService svc, PriceSettingsStore settingsStore, string area = "DK1") =>
{
    var settings = settingsStore.Get();
    var prices = await svc.GetPricesAsync(area);
    var gridPreset = GridCompanyPreset.All.FirstOrDefault(g => g.Id == settings.GridCompanyId);
    var gridMonthlyKr = gridPreset?.Id == "custom" ? 0 : gridPreset?.MonthlySubscriptionKr ?? 0;

    var results = SupplierPreset.All
        .Where(s => s.Id != "custom")
        .Select(supplier =>
        {
            var trial = new PriceSettings
            {
                GridCompanyId = settings.GridCompanyId,
                CustomNettarifOre = settings.CustomNettarifOre,
                SupplierId = supplier.Id,
                AnnualUsageKwh = settings.AnnualUsageKwh,
            };

            var avgTotal = prices.Select(p => PriceBreakdownCalculator.Compute(p, trial)).Average(h => h.TotalKrPerKwh);
            var estYearlyKr = avgTotal * settings.AnnualUsageKwh + (supplier.MonthlySubscriptionKr + gridMonthlyKr) * 12;

            return new
            {
                supplier.Id,
                supplier.Name,
                AvgTotalKrPerKwh = Math.Round(avgTotal, 3),
                EstYearlyCostKr = Math.Round(estYearlyKr, 0),
                IsCurrent = supplier.Id == settings.SupplierId,
            };
        })
        .OrderBy(r => r.EstYearlyCostKr)
        .ToList();

    return Results.Ok(results);
});

// ── Run log (device start tracking + cost) ──────────────────────────────────

app.MapPost("/api/runs", async (ElectricityPriceService priceSvc, PriceSettingsStore settingsStore, RunLogStore runs, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<LogRunBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Device) || body.EstKwh <= 0)
        return Results.BadRequest();

    var settings = settingsStore.Get();
    var prices = await priceSvc.GetPricesAsync(body.Area ?? "DK1");
    var now = prices
        .Where(p => p.TimeDk <= DateTime.Now)
        .OrderByDescending(p => p.TimeDk)
        .FirstOrDefault();

    var currentPrice = now is null ? 0 : PriceBreakdownCalculator.Compute(now, settings).TotalKrPerKwh;
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
