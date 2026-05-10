using ITMartinBudget.Application;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// RAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// =========================
// DATABASE
// =========================
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration
            .GetConnectionString("Default")
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
    ITransactionGroupingService,
    TransactionGroupingService>();

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

builder.Services.AddScoped<
    ITransactionProcessor,
    TransactionProcessor>();

// =========================
// IMPORT / RULES
// =========================
builder.Services.AddScoped<
    BankTransactionCsvService>();

builder.Services.AddScoped<
    CategoryRuleStartupService>();

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

    var seeder =
        scope.ServiceProvider
            .GetRequiredService<CategoryRuleStartupService>();

    await seeder.SeedAsync();
}

// =========================
// MIDDLEWARE
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

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