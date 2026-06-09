using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Runtime.Registry;

public sealed class WorkflowRegistry(
    IServiceProvider serviceProvider)
    : IWorkflowRegistry
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider;

    public IWorkflowDefinition Resolve(
        WorkflowType workflowType)
    {
        return workflowType switch
        {
            WorkflowType.Package1 =>
                _serviceProvider.GetRequiredService<
                    Package1WorkflowDefinition>(),

            WorkflowType.Package2 =>
                _serviceProvider.GetRequiredService<
                    Package2WorkflowDefinition>(),

            _ =>
                throw new InvalidOperationException(
                    $"Workflow '{workflowType}' not registered.")
        };
    }
}