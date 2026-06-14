using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartPackage2Handler
    : IBackgroundJobHandler
{
    private readonly Package2WorkflowOrchestrator _orchestrator;
    private readonly Package2WorkflowRunner _runner;

    public string JobType =>
        BackgroundJobTypes.StartPackage2;

    public StartPackage2Handler(
        Package2WorkflowOrchestrator orchestrator,
        Package2WorkflowRunner runner)
    {
        _orchestrator = orchestrator;
        _runner = runner;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var request =
            JsonSerializer.Deserialize<
                StartPackage2Request>(
                job.Payload);

        if (request is null)
        {
            return;
        }

        var result =
            await _orchestrator.StartAsync(
                request,
                cancellationToken);

        await _runner.ExecuteAsync(
            result.WorkflowId,
            result.State,
            cancellationToken);
    }
}