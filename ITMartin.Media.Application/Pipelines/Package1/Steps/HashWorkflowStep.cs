using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class HashWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<HashWorkflowStep>
        _logger;

    private readonly IHashService
        _hashService;

    public HashWorkflowStep(
        ILogger<HashWorkflowStep> logger,
        IHashService hashService)
    {
        _logger =
            logger;

        _hashService =
            hashService;
    }

    public override string Name =>
        "Hashing";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var total =
            state.MediaFiles.Count;

        var current = 0;

        foreach (var file in state.MediaFiles)
        {
            current++;

            LogStepProgress(
                _logger,
                Name,
                current,
                total,
                file.FileName);

            if (!string.IsNullOrWhiteSpace(
                    file.Hash))
            {
                continue;
            }

            await ExecuteOperationAsync(
                "HashFile",
                file.FileName,
                async () =>
                {
                    var hash =
                        _hashService.ComputeHash(
                            file.FullPath);

                    file.SetHash(hash);

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}