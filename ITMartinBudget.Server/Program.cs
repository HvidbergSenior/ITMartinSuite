using ITMartinBudget.Application.Services;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using ITMartinBudgetInfrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<BudgetDbContext>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<BankTransactionCsvService>();
builder.Services.AddScoped<BudgetService>();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();