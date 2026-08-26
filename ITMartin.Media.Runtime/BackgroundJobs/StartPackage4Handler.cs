using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Pipelines.Package4.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartPackage4Handler : IBackgroundJobHandler
{
    private readonly Package4WorkflowOrchestrator _orchestrator;
    private readonly Package4WorkflowRunner _runner;

    public string JobType => BackgroundJobTypes.StartPackage4;

    public StartPackage4Handler(Package4WorkflowOrchestrator orchestrator, Package4WorkflowRunner runner)
    {
        _orchestrator = orchestrator;
        _runner = runner;
    }

    public async Task HandleAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<StartPackage4Request>(job.Payload);

        if (request is null)
        {
            return;
        }

        var result = await _orchestrator.StartAsync(request, cancellationToken);

        await _runner.ExecuteAsync(result.WorkflowId, result.State, cancellationToken);
    }
}
