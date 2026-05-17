using ITMartin.Media.Application.Workflow.Models;

namespace ITMartin.Media.Application.Workflow.Abstractions;

public interface IWorkflowStep
{
    string Name { get; }

    Task ExecuteAsync(
        WorkflowStepContext context);
}