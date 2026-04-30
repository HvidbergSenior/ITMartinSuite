using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ✅ Database
builder.Services.AddDbContext<BudgetDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ✅ Services
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<BankTransactionCsvService>();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();

    if (!db.CategoryRules.Any())
    {
        var rules = new[]
        {
            // 🏠 SAVINGS / TRANSFERS
            ("boligopsparing", SubCategory.Opsparing),
            ("opsparing", SubCategory.Opsparing),

            // ✈️ TRAVEL
            ("hotel", SubCategory.Rejser),
            ("booking", SubCategory.Rejser),
            ("airbnb", SubCategory.Rejser),

            // 👕 SHOPPING
            ("arket", SubCategory.Tøj),
            ("zara", SubCategory.Tøj),
            ("hm", SubCategory.Tøj),

            // ✂️ PERSONAL
            ("frisør", SubCategory.Frisør),

            // 🍔 FOOD
            ("pizza", SubCategory.Restaurant),
            ("justeat", SubCategory.Fastfood),
            ("mcdonald", SubCategory.Fastfood),

            // 📱 SUBSCRIPTIONS
            ("netflix", SubCategory.StreamingTjenester),
            ("spotify", SubCategory.StreamingTjenester),

            // 🎮 PERSONAL / GAMBLING / RANDOM
            ("sport ventures", SubCategory.PersonligtForbrug),
            ("bet365", SubCategory.PersonligtForbrug),

            // 🚗 TRANSPORT
            ("ok benzin", SubCategory.Benzin),
            ("circle k", SubCategory.Benzin),
            ("dsb", SubCategory.OffentligTransport),
        };

        db.CategoryRules.AddRange(
            rules.Select(r => new CategoryRule
            {
                Keyword = KeywordNormalizer.Normalize(r.Item1),
                SubCategory = r.Item2,
                Priority = 10,
                IsActive = true
            })
        );

        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();