using ITMartinImageProcessor.Application.Handlers;
using ITMartinImageProcessor.Application.Services;
using ITMartinImageProcessor.Domain.Interfaces;
using ITMartinImageProcessor.Infrastructure;
using ITMartinImageProcessor.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

services.AddSingleton<IConfiguration>(config);
services.AddScoped<IFileService, FileService>();
services.AddScoped<IImageProcessor, ImageSharpProcessor>(); // 🔥 THIS IS MISSING
services.AddScoped<ProcessImagesHandler>();
services.AddScoped<HttpUploadService>();
var provider = services.BuildServiceProvider();

// ENV overrides config (Docker)
var input = Environment.GetEnvironmentVariable("INPUT_PATH") ?? config["Paths:Input"];
var output = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? config["Paths:Output"];
var archive = Environment.GetEnvironmentVariable("ARCHIVE_PATH") ?? config["Paths:Archive"];

Console.WriteLine("======================================");
Console.WriteLine($"[PATH] INPUT: {input}");
Console.WriteLine($"[PATH] OUTPUT: {output}");
Console.WriteLine($"[PATH] ARCHIVE: {archive}");
Console.WriteLine("======================================");

while (true)
{
    try
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessImagesHandler>();

        await handler.ExecuteAsync(input!, output!, archive!);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[LOOP ERROR] {ex}");
    }

    await Task.Delay(5000);
}