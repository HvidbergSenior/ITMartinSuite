using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Workflow.Models;
using ITMartin.Media.Application.Workflow.Steps;
using Microsoft.Extensions.DependencyInjection;
using WorkflowDefinition = ITMartin.Media.Application.Abstractions.Workflows.WorkflowDefinition;

namespace ITMartin.Media.Application.Abstractions.Orchestration;

public sealed class Package1WorkflowOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowExecutor _workflowExecutor;
    public Package1WorkflowOrchestrator(
        IServiceProvider serviceProvider, IWorkflowExecutor workflowExecutor)
    {
        _serviceProvider = serviceProvider;
        _workflowExecutor = workflowExecutor;
    }

    public async Task ExecuteAsync(
        Guid sessionId,
        string rootPath,
        CancellationToken cancellationToken)
    {
        var workflow =
            new WorkflowDefinition
            {
                Name = "Package1Workflow",
                Steps =
                [
                    _serviceProvider.GetRequiredService<FileDiscoveryWorkflowStep>(),
                    _serviceProvider.GetRequiredService<HashWorkflowStep>(),
                    _serviceProvider.GetRequiredService<MetadataWorkflowStep>()
                ]
            };

        var context =
            new WorkflowExecutionContext
            {
                WorkflowId = sessionId,
                WorkflowName = workflow.Name,
                CancellationToken = cancellationToken,
                Items =
                {
                    ["RootPath"] = rootPath
                }
            };

        await _workflowExecutor.ExecuteAsync(
            workflow,
            context,
            cancellationToken);
    }
}