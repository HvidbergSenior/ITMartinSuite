using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class HashWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<HashWorkflowStep>
        _logger;

    private readonly IHashService
        _hashService;

    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    public HashWorkflowStep(
        ILogger<HashWorkflowStep> logger,
        IHashService hashService,
        IWorkflowInstanceStore workflowInstanceStore)
    {
        _logger =
            logger;

        _hashService =
            hashService;

        _workflowInstanceStore =
            workflowInstanceStore;
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

            if (current % 10 == 0 || current == total)
            {
                await _workflowInstanceStore.SetProgressAsync(
                    context.WorkflowId,
                    current,
                    total,
                    item: file.FileName,
                    cancellationToken: cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(
                    file.Hash))
            {
                continue;
            }

            var ok = await ExecuteOperationAsync(
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

            if (!ok)
                state.FailedFiles.Add(new FailedFile { FilePath = file.FullPath, Step = Name, Error = "Hash failed" });
        }
    }
}