using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Infrastructure;
using ITMartinR6Assistant.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IR6DataService, R6DataService>();
builder.Services.AddSingleton<SessionStateService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
