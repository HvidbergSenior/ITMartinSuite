using ITMartinR6Strat.Server.Hubs;
using ITMartinR6Strat.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddHttpClient("claude");
builder.Services.AddSingleton<StratSessionService>();
builder.Services.AddSingleton<StratAiService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();
app.MapHub<StratHub>("/hubs/strat");
app.MapRazorComponents<ITMartinR6Strat.Server.App>()
   .AddInteractiveServerRenderMode();

app.Run();
