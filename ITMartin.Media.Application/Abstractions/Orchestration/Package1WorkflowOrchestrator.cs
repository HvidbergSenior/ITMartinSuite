using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Models;
using ITMartin.Media.Application.Workflow.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Application.Abstractions.Orchestration;

public sealed class Package1WorkflowOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowEngine _workflowEngine;

    public Package1WorkflowOrchestrator(
        IServiceProvider serviceProvider,
        IWorkflowEngine workflowEngine)
    {
        _serviceProvider = serviceProvider;
        _workflowEngine = workflowEngine;
    }

    public async Task ExecuteAsync(
        Guid sessionId,
        string rootPath,
        CancellationToken cancellationToken)
    {
        var definition = new WorkflowDefinition
        {
            Name = "Package1Workflow",
            Steps =
            [
                _serviceProvider.GetRequiredService<FileDiscoveryWorkflowStep>(),
                _serviceProvider.GetRequiredService<HashWorkflowStep>(),
                _serviceProvider.GetRequiredService<MetadataWorkflowStep>()
            ]
        };

        var context = new WorkflowStepContext
        {
            SessionId = sessionId,
            RootPath = rootPath,
            CancellationToken = cancellationToken
        };

        await _workflowEngine.ExecuteAsync(
            definition,
            context);
    }
}