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

var adminPin = app.Configuration["Budget__AdminPin"] ?? "budget2025";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var bypass = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase);
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