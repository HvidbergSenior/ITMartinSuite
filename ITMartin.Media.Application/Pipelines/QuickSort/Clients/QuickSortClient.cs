using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.QuickSort;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Clients;

public sealed class QuickSortClient
    : IQuickSortClient
{
    private readonly IBackgroundJobQueue
        _backgroundJobQueue;

    public QuickSortClient(
        IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue =
            backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(
        StartQuickSortRequest request,
        CancellationToken cancellationToken)
    {
        var workflowId =
            Guid.NewGuid();

        var state =
            new QuickSortWorkflowState
            {
                RootPath =
                    request.SourceLibraryPath,
                OverrideYear = request.OverrideYear,
                OutputPath = request.OutputPath,
                EnableAiClassification = request.EnableAiClassification,
                EnableDeduplication = request.EnableDeduplication,
                EnableBaselineSnapshot = request.EnableBaselineSnapshot
            };

        await _backgroundJobQueue
            .EnqueueAsync(
                new BackgroundJob
                {
                    Id = workflowId,

                    Queue = "workflow",

                    Type = "StartQuickSort",

                    Payload =
                        JsonSerializer.Serialize(
                            state),

                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

        return workflowId;
    }
}