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
    ITransactionCategorizer,
    TransactionCategorizer>();
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