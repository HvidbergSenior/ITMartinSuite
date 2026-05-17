using ITMartin.Media.Application.Workflow.Models;

namespace ITMartin.Media.Application.Workflow.Abstractions;

public interface IWorkflowEngine
{
    Task ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowStepContext context);
}