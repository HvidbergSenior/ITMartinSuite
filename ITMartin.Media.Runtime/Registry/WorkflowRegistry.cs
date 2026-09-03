using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;
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
            WorkflowType.QuickSort =>
                _serviceProvider.GetRequiredService<
                    QuickSortWorkflowDefinition>(),

            WorkflowType.AnalogDigitize =>
                _serviceProvider.GetRequiredService<
                    AnalogDigitizeWorkflowDefinition>(),

            _ =>
                throw new InvalidOperationException(
                    $"Workflow '{workflowType}' not registered.")
        };
    }
}