using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Application.Services;
using ITMartinR6Assistant.Infrastructure.Repositories;
using ITMartinR6Assistant.Server;
using ITMartinR6Assistant.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddScoped<RecommendationService>();

builder.Services.AddScoped<IRecommendationRepository,
    JsonRecommendationRepository>();

builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();