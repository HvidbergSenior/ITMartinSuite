using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Clients;

public sealed class AnalogDigitizeClient
    : IAnalogDigitizeClient
{
    private readonly IBackgroundJobQueue
        _backgroundJobQueue;

    public AnalogDigitizeClient(
        IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue =
            backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(
        StartAnalogDigitizeRequest request,
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

                    Type = "StartAnalogDigitize",

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