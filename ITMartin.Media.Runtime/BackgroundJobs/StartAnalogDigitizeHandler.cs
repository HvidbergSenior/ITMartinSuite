using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartAnalogDigitizeHandler
    : IBackgroundJobHandler
{
    private readonly AnalogDigitizeWorkflowOrchestrator _orchestrator;
    private readonly AnalogDigitizeWorkflowRunner _runner;

    public string JobType =>
        BackgroundJobTypes.StartAnalogDigitize;

    public StartAnalogDigitizeHandler(
        AnalogDigitizeWorkflowOrchestrator orchestrator,
        AnalogDigitizeWorkflowRunner runner)
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
                StartAnalogDigitizeRequest>(
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