using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

public sealed class ScanClient
    : IScanClient
{
    private readonly IBackgroundJobQueue
        _backgroundJobQueue;

    public ScanClient(
        IBackgroundJobQueue backgroundJobQueue)
    {
        _backgroundJobQueue =
            backgroundJobQueue;
    }

    public async Task<Guid> StartAsync(
        StartScanRequest request,
        CancellationToken cancellationToken)
    {
        var workflowId =
            Guid.NewGuid();

        await _backgroundJobQueue.EnqueueAsync(
            new BackgroundJob
            {
                Id = workflowId,
                Queue = "workflow",
                Type = "StartScan",
                Payload =
                    System.Text.Json.JsonSerializer
                        .Serialize(request)
            },
            cancellationToken);

        return workflowId;
    }
}