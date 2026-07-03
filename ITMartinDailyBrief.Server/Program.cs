using ITMartinDailyBrief.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("feed", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("DailyBrief/1.0 (+https://itmartin.dk)");
});

builder.Services.AddSingleton<FeedService>();
builder.Services.AddSingleton<BriefingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinDailyBrief.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
