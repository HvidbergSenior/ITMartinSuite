using ITMartinR6Intel.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IntelDataService>();
builder.Services.AddSingleton<IntelSessionService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinR6Intel.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
