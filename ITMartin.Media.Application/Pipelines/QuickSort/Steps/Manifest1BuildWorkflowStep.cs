using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class Manifest1BuildWorkflowStep
    : QuickSortWorkflowStepBase
{
    private readonly QuickSortManifestBuilder
        _manifestBuilder;

    private readonly ILogger<
            Manifest1BuildWorkflowStep>
        _logger;

    private readonly IQuickSortManifestStore
        _manifestStore;

    public Manifest1BuildWorkflowStep(
        QuickSortManifestBuilder manifestBuilder,
        ILogger<Manifest1BuildWorkflowStep> logger,
        IQuickSortManifestStore manifestStore)
    {
        _manifestBuilder =
            manifestBuilder;

        _logger =
            logger;

        _manifestStore =
            manifestStore;
    }

    public override string Name =>
        "Manifest";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        await ExecuteOperationAsync(
            "BuildManifest",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var manifest =
                    _manifestBuilder.Build(
                        context.WorkflowId,
                        state);

                state.Manifest =
                    manifest;

                await _manifestStore.SaveAsync(
                    manifest,
                    cancellationToken);
            },
            _logger);
    }
}