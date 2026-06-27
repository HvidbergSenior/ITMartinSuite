using ITMartin.Ai;
using ITMartin.FamilieOverblik.Infrastructure;
using ITMartin.Magic.Application;
using ITMartin.Magic.Infrastructure;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.OCR;
using ITMartin.Receipt.Application;
using ITMartin.Receipt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(
    options =>
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "ITMartin API",
                Version = "v1"
            });
    });

builder.Services.AddAi();
builder.Services.AddOcr();
builder.Services.AddMagicInfrastructure(builder.Configuration);

builder.Services.AddMediaPlatform(
    builder.Configuration);

builder.Services.AddMagicApplication(builder.Configuration);

builder.Services.AddReceiptApplication();
builder.Services.AddReceiptInfrastructure(builder.Configuration);

// =========================
// FAMILIE OVERBLIK
// =========================
var familieDb = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("FamilieDb") ?? "Data Source=familie.db"
    : "Data Source=/app/data/familie.db";

builder.Services.AddDbContext<FamilieOverblikDbContext>(options =>
    options.UseSqlite(familieDb));

builder.Services.AddScoped<FamilyTaskService>();

builder.Services.AddDbContext<MediaDbContext>(options =>
{
    options.UseSqlite(
        builder.Configuration.GetConnectionString("MediaDb")
        ?? "Data Source=media.db");
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider
        .GetRequiredService<FamilieOverblikDbContext>()
        .Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseCors();

app.MapControllers();

app.Run();