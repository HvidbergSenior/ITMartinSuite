using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITMartin.Media.Infrastructure.Workers;

public sealed class ThumbnailWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ThumbnailWorker(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();

            var queue = scope.ServiceProvider
                .GetRequiredService<IBackgroundJobQueue>();

            var strategy = scope.ServiceProvider
                .GetRequiredService<IThumbnailStrategy>();

            var job = await queue.DequeueAsync(
                "thumbnails",
                stoppingToken);

            if (job is null)
            {
                await Task.Delay(1000, stoppingToken);

                continue;
            }

            var payload = JsonSerializer.Deserialize<ThumbnailJob>(
                job.Payload);

            if (payload is null)
            {
                continue;
            }

            await strategy.GenerateAsync(
                payload.FilePath,
                stoppingToken);
        }
    }
}