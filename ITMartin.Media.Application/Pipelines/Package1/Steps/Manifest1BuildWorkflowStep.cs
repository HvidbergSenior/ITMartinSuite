using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class Manifest1BuildWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly Package1ManifestBuilder
        _manifestBuilder;

    private readonly ILogger<
            Manifest1BuildWorkflowStep>
        _logger;

    private readonly IPackage1ManifestStore
        _manifestStore;

    public Manifest1BuildWorkflowStep(
        Package1ManifestBuilder manifestBuilder,
        ILogger<Manifest1BuildWorkflowStep> logger,
        IPackage1ManifestStore manifestStore)
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
            context.State as Package1WorkflowState
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