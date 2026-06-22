using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Application.Pipelines.Package2.Clients;

public sealed class Package2Client
    : IPackage2Client
{
    private readonly IBackgroundJobQueue
        _backgroundJobQueue;

    public Package2Client(
        IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue =
            backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(
        StartPackage2Request request,
        CancellationToken cancellationToken)
    {
        var workflowId =
            Guid.NewGuid();
        Console.WriteLine("PACKAGE2 CLICKED");
        await _backgroundJobQueue
            .EnqueueAsync(
                new BackgroundJob
                {
                    Id = workflowId,

                    Queue = "workflow",

                    Type = "StartPackage2",

                    Payload =
                        JsonSerializer
                            .Serialize(request),

                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);
        Console.WriteLine("PACKAGE2 ENQUEUED");
        return workflowId;
    }
}