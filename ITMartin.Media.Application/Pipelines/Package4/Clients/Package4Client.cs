using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

namespace ITMartin.Media.Application.Pipelines.Package4.Clients;

public sealed class Package4Client : IPackage4Client
{
    private readonly IBackgroundJobQueue _backgroundJobQueue;

    public Package4Client(IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue = backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(StartPackage4Request request, CancellationToken cancellationToken)
    {
        var workflowId = Guid.NewGuid();

        await _backgroundJobQueue.EnqueueAsync(
            new BackgroundJob
            {
                Id = workflowId,
                Queue = "workflow",
                Type = "StartPackage4",
                Payload = JsonSerializer.Serialize(request),
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        return workflowId;
    }
}
