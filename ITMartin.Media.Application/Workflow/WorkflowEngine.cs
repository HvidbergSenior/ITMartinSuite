using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Models;

namespace ITMartin.Media.Application.Workflow;

public sealed class WorkflowEngine : IWorkflowEngine
{
    public async Task ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowStepContext context)
    {
        foreach (var step in definition.Steps)
        {
            await step.ExecuteAsync(context);
        }
    }
}