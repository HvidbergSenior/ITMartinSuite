using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ManifestBuildWorkflowStep
    : IWorkflowStep
{
    private readonly Package1ManifestBuilder
        _manifestBuilder;

    private readonly ILogger<
            ManifestBuildWorkflowStep>
        _logger;
    private readonly IPackage1ManifestStore
        _manifestStore;

    public ManifestBuildWorkflowStep(
        Package1ManifestBuilder manifestBuilder,
        ILogger<ManifestBuildWorkflowStep> logger, IPackage1ManifestStore manifestStore)
    {
        _manifestBuilder = manifestBuilder;
        _logger = logger;
        _manifestStore = manifestStore;
    }

    public string Name => "Manifest";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(ManifestBuildWorkflowStep));
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");
        _logger.LogInformation(
            "MediaFiles count: {Count}",
            state.MediaFiles.Count);
        _logger.LogInformation(
            "Building manifest");

        var manifest =
            _manifestBuilder.Build(
                context.WorkflowId,
                state);

        state.Manifest = manifest;

        await _manifestStore.SaveAsync(
            manifest,
            cancellationToken);
    }
}