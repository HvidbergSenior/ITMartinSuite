using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Application.Services;
using ITMartinR6Assistant.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<IRecommendationRepository, JsonRecommendationRepository>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/maps", async (RecommendationService svc) =>
    Results.Ok(await svc.GetMaps()));

app.MapGet("/api/maps/{name}", async (string name, RecommendationService svc) =>
    Results.Ok(await svc.GetRecommendations(name)));

app.Run();
