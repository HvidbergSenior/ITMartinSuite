using ITMartinBudget.Application;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Razor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("Default")
        : "Data Source=/app/data/budget.db";

builder.Services.AddDbContext<BudgetDbContext>(options =>
    options.UseSqlite(connectionString));

// Services
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<ITransactionGroupingService, TransactionGroupingService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionProcessor, TransactionProcessor>();

// CSV
builder.Services.AddScoped<BankTransactionCsvService>();

// Logging
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.Warning);

var app = builder.Build();

// Database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();
}

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();