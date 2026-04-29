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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();

    if (!db.CategoryRules.Any())
    {
        db.CategoryRules.AddRange(new[]
        {
            // 🛒 FOOD
            new CategoryRule { Keyword = "netto", SubCategory = SubCategory.Dagligvarer, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "rema", SubCategory = SubCategory.Dagligvarer, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "føtex", SubCategory = SubCategory.Dagligvarer, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "bilka", SubCategory = SubCategory.Dagligvarer, Priority = 10, IsActive = true },

            new CategoryRule { Keyword = "mcd", SubCategory = SubCategory.Restaurant, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "pizza", SubCategory = SubCategory.Restaurant, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "wolt", SubCategory = SubCategory.Restaurant, Priority = 10, IsActive = true },

            // 📱 SUBSCRIPTIONS
            new CategoryRule { Keyword = "spotify", SubCategory = SubCategory.StreamingTjenester, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "netflix", SubCategory = SubCategory.StreamingTjenester, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "youtube", SubCategory = SubCategory.StreamingTjenester, Priority = 10, IsActive = true },

            new CategoryRule { Keyword = "internet", SubCategory = SubCategory.Internet, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "telia", SubCategory = SubCategory.Telefonabonnement, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "telenor", SubCategory = SubCategory.Telefonabonnement, Priority = 10, IsActive = true },

            // 🚗 TRANSPORT
            new CategoryRule { Keyword = "shell", SubCategory = SubCategory.Benzin, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "circle k", SubCategory = SubCategory.Benzin, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "ok tank", SubCategory = SubCategory.Benzin, Priority = 10, IsActive = true },

            new CategoryRule { Keyword = "parkering", SubCategory = SubCategory.Parkering, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "dsb", SubCategory = SubCategory.OffentligTransport, Priority = 10, IsActive = true },

            // 🏠 HOUSING
            new CategoryRule { Keyword = "realkredit", SubCategory = SubCategory.RenterLån, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "husleje", SubCategory = SubCategory.Husleje, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "kredsløb", SubCategory = SubCategory.VarmeVandAffald, Priority = 10, IsActive = true },

            // 🏥 HEALTH
            new CategoryRule { Keyword = "apotek", SubCategory = SubCategory.Medicin, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "tandlæge", SubCategory = SubCategory.Tandlæge, Priority = 10, IsActive = true },

            // 📊 FINANCE
            new CategoryRule { Keyword = "forsikring", SubCategory = SubCategory.Forsikring, Priority = 10, IsActive = true },
            new CategoryRule { Keyword = "fagforening", SubCategory = SubCategory.FagforeningAkasse, Priority = 10, IsActive = true }
        });

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