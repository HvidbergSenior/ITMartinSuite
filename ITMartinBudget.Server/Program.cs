using ITMartinBudget.Application;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using ITMartinBudget.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// RAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ToastService>();

// =========================
// DATABASE
// =========================
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration
            .GetConnectionString("BudgetDb")
        : "Data Source=/app/data/budget.db";

Console.WriteLine($"DB: {connectionString}");
builder.Services.AddDbContext<BudgetDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// =========================
// APPLICATION SERVICES
// =========================
builder.Services.AddScoped<
    IBudgetService,
    BudgetService>();
builder.Services.AddScoped<
    IDashboardService,
    DashboardService>();
builder.Services.AddScoped<
    ITransactionCategorizer,
    TransactionCategorizer>();
builder.Services.AddScoped<
    IForwardBudgetService,
    ForwardBudgetService>();
builder.Services.AddScoped<
    ISubscriptionDetectionService,
    SubscriptionDetectionService>();
// =========================
// AI
// =========================
builder.Services.AddScoped<
    IClaudeTransactionCategorizationService,
    ClaudeTransactionCategorizationService>();
builder.Services.AddScoped<
    IFinancialAdvisorService,
    ClaudeFinancialAdvisorService>();
builder.Services.AddScoped<
    IPlannedTransactionService,
    PlannedTransactionService>();
// =========================
// FORECAST
// =========================
builder.Services.AddScoped<
    IFinancialForecastService,
    FinancialForecastService>();
// =========================
// IMPORT / RULES
// =========================
builder.Services.AddScoped<
    BankTransactionCsvService>();
// =========================
// LOGGING
// =========================
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.Warning);

// =========================
// BUILD
// =========================
var app = builder.Build();

// =========================
// DATABASE INIT
// =========================
using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();

}

// =========================
// MIDDLEWARE
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

var adminPin = app.Configuration["Budget:AdminPin"] ?? "budget2025";

// Long random secret for the no-login "quick overview" - deliberately not the
// 4-digit admin PIN, since this link gets shared and saved to a device instead
// of typed. Bootstrapping via /quick/{token} stores it in localStorage; after
// that /quick and /api/quick/* validate it themselves, so both are excluded
// from the PIN-cookie gate below.
var quickToken = app.Configuration["Budget:QuickToken"] ?? "qk-8f2a1c6d9b3e4f0a7c5d2e1b6a9f3c8d";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var bypass = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/quick", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/api/quick", StringComparison.OrdinalIgnoreCase);
    if (bypass || (ctx.Request.Cookies.TryGetValue("budget_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [Microsoft.AspNetCore.Mvc.FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("budget_auth", adminPin, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            MaxAge   = TimeSpan.FromDays(30)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("budget_auth");
    return Results.Redirect("/login");
});

app.MapGet("/api/quick/overview", async (string? token, IDashboardService dashboardService) =>
{
    if (token != quickToken) return Results.Unauthorized();

    var dashboard = await dashboardService.BuildDashboardAsync();
    var now = DateTime.Now;
    var currentMonthTx = dashboard.Transactions
        .Where(x => x.Date.Month == now.Month && x.Date.Year == now.Year)
        .ToList();

    var income = currentMonthTx.Where(x => x.Amount > 0).Sum(x => x.Amount);
    var expenses = Math.Abs(currentMonthTx.Where(x => x.Amount < 0).Sum(x => x.Amount));

    return Results.Ok(new
    {
        month = now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("da-DK")),
        income,
        expenses,
        net = income - expenses
    });
});

app.MapGet("/api/quick/subscriptions", async (string? token, ISubscriptionDetectionService subscriptionService) =>
{
    if (token != quickToken) return Results.Unauthorized();

    var list = await subscriptionService.DetectAsync();
    return Results.Ok(list.Select(s => new
    {
        s.Amount,
        s.IntervalLabel,
        s.Occurrences,
        s.LastChargedDate,
        s.DaysSinceLastCharge,
        s.SampleDescription
    }));
});

app.UseAntiforgery();

// =========================
// BLAZOR
// =========================
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================
app.Run();