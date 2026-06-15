using ITMartin.Ai;
using ITMartin.Magic.Application;
using ITMartin.Magic.Infrastructure;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.OCR;
using ITMartin.Receipt.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

builder.Services.AddDbContext<MediaDbContext>(options =>
{
    options.UseSqlite(
        builder.Configuration.GetConnectionString("MediaDb")
        ?? "Data Source=media.db");
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();