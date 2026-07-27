using ITMartinRewlhul.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<RewlhulBroadcastService>();
builder.Services.AddSingleton<GameRoomService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinRewlhul.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
