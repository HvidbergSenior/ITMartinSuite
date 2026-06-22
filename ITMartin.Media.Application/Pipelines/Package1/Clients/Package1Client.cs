using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1;

namespace ITMartin.Media.Application.Pipelines.Package1.Clients;

public sealed class Package1Client
    : IPackage1Client
{
    private readonly IBackgroundJobQueue
        _backgroundJobQueue;

    public Package1Client(
        IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue =
            backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(
        StartPackage1Request request,
        CancellationToken cancellationToken)
    {
        var workflowId =
            Guid.NewGuid();

        var state =
            new Package1WorkflowState
            {
                RootPath =
                    request.SourceLibraryPath,
                OverrideYear = request.OverrideYear,
                OutputPath = request.OutputPath,
                EnableAiClassification = request.EnableAiClassification
            };

        await _backgroundJobQueue
            .EnqueueAsync(
                new BackgroundJob
                {
                    Id = workflowId,

                    Queue = "workflow",

                    Type = "StartPackage1",

                    Payload =
                        JsonSerializer.Serialize(
                            state),

                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

        return workflowId;
    }
}