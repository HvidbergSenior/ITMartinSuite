using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
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

    if (!db.CategoryRules.Any())
    {
        db.CategoryRules.AddRange(new[]
        {
            new CategoryRule { Keyword = "netto", SubCategory = SubCategory.Dagligvarer, Priority = 10 },
            new CategoryRule { Keyword = "rema", SubCategory = SubCategory.Dagligvarer, Priority = 10 },
            new CategoryRule { Keyword = "føtex", SubCategory = SubCategory.Dagligvarer, Priority = 10 },

            new CategoryRule { Keyword = "mcd", SubCategory = SubCategory.Restaurant, Priority = 10 },
            new CategoryRule { Keyword = "pizza", SubCategory = SubCategory.Restaurant, Priority = 10 },

            new CategoryRule { Keyword = "spotify", SubCategory = SubCategory.Abonnement, Priority = 10 },
            new CategoryRule { Keyword = "netflix", SubCategory = SubCategory.Abonnement, Priority = 10 },

            new CategoryRule { Keyword = "shell", SubCategory = SubCategory.Benzin, Priority = 10 },
            new CategoryRule { Keyword = "circle k", SubCategory = SubCategory.Benzin, Priority = 10 },
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