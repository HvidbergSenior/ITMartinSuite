using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowOrchestrator
{
    private readonly Package2WorkflowFactory
        _factory;

    private readonly Package2WorkflowDefinition
        _workflowDefinition;

    private readonly ILogger<
            Package2WorkflowOrchestrator>
        _logger;

    private readonly Package1ManifestLoader
        _manifestLoader;

    public Package2WorkflowOrchestrator(
        Package2WorkflowFactory factory,
        Package2WorkflowDefinition workflowDefinition,
        ILogger<Package2WorkflowOrchestrator> logger,
        Package1ManifestLoader manifestLoader)
    {
        _factory = factory;

        _workflowDefinition = workflowDefinition;

        _logger = logger;

        _manifestLoader = manifestLoader;
    }

    public async Task RunAsync(
        StartPackage2Request request,
        CancellationToken cancellationToken)
    {
        var manifest =
            await _manifestLoader.LoadAsync(
                request.SourceLibraryPath,
                cancellationToken);

        var state =
            _factory.Create(
                manifest,
                request);

        foreach (var step in _workflowDefinition.Steps)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Executing Package2 step {StepName}",
                step.Name);

            var context =
                new WorkflowExecutionContext<
                    Package2WorkflowState>
                {
                    WorkflowId = Guid.NewGuid(),

                    WorkflowName = "Package2",

                    State = state
                };

            await step.ExecuteAsync(
                context,
                cancellationToken);

            cancellationToken
                .ThrowIfCancellationRequested();
        }

        _logger.LogInformation(
            "Package2 completed");
    }
}