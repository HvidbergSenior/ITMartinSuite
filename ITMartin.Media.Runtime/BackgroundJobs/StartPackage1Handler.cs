using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartPackage1Handler
    : IBackgroundJobHandler
{
    private readonly IScanOrchestrator _orchestrator;
    private readonly Package1WorkflowRunner _runner;

    public string JobType =>
        BackgroundJobTypes.StartPackage1;

    public StartPackage1Handler(
        IScanOrchestrator orchestrator,
        Package1WorkflowRunner runner)
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
                Package1WorkflowState>(
                job.Payload);

        if (request is null)
        {
            return;
        }

        var workflowId =
            await _orchestrator.StartAsync(
                request,
                cancellationToken);

        await _runner.ExecuteAsync(
            workflowId,
            request,
            cancellationToken);
    }
}