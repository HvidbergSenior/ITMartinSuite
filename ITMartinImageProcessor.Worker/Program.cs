using ITMartinImageProcessor.Application.Handlers;
using ITMartinImageProcessor.Application.Services;
using ITMartinImageProcessor.Domain.Interfaces;
using ITMartinImageProcessor.Infrastructure;
using ITMartinImageProcessor.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddScoped<IImageProcessor, ImageSharpProcessor>();
services.AddScoped<IFileService, FileService>();
services.AddScoped<ProcessNewImagesHandler>();
services.AddScoped<HeicToJpgConverterService>();
var provider = services.BuildServiceProvider();

while (true)
{
    using var scope = provider.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<ProcessNewImagesHandler>();

    await handler.ExecuteAsync(
        "/data/input",
        "/data/ready",
        "/data/archive"
    );

    await Task.Delay(5000); // every 5 seconds
}