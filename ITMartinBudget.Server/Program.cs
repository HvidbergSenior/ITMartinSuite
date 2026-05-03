using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Razor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 🔹 Database
builder.Services.AddDbContext<BudgetDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// 🔹 Core services
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<ITransactionGroupingService, TransactionGroupingService>();
builder.Services.AddScoped<ICategoryRuleRepository, CategoryRuleRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<INameNormalizer, NameNormalizer>();
builder.Services.AddScoped<ITransactionProcessor, TransactionProcessor>(); // ✅ THIS LINE
// 🔹 CSV import
builder.Services.AddScoped<BankTransactionCsvService>();

// 🔹 Logging (optional tuning)
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

var app = builder.Build();


// 🔥 DATABASE SEEDING
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();

    if (!db.CategoryRules.Any())
    {
        var rules = new[]
        {
            ("boligopsparing", SubCategory.Opsparing),
            ("opsparing", SubCategory.Opsparing),

            ("hotel", SubCategory.Rejser),
            ("booking", SubCategory.Rejser),
            ("airbnb", SubCategory.Rejser),

            ("arket", SubCategory.Tøj),
            ("zara", SubCategory.Tøj),
            ("hm", SubCategory.Tøj),

            ("frisør", SubCategory.Frisør),

            ("pizza", SubCategory.Restaurant),
            ("justeat", SubCategory.Fastfood),
            ("mcdonald", SubCategory.Fastfood),

            ("netflix", SubCategory.StreamingTjenester),
            ("spotify", SubCategory.StreamingTjenester),

            ("sport ventures", SubCategory.PersonligtForbrug),
            ("bet365", SubCategory.PersonligtForbrug),

            ("ok benzin", SubCategory.Benzin),
            ("circle k", SubCategory.Benzin),
            ("dsb", SubCategory.OffentligTransport),
        };

        db.CategoryRules.AddRange(
            rules.Select(r => new CategoryRule
            {
                Keyword = r.Item1.ToLowerInvariant(), // ✅ safer than external normalizer here
                SubCategory = r.Item2,
                Priority = 10,
                IsActive = true,
                IsVerified = true
            })
        );

        db.SaveChanges();
    }
}


// 🔹 Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

// 🔹 Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();