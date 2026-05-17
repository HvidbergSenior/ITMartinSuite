using ITMartin.Media.Application.Workflow.Abstractions;

namespace ITMartin.Media.Application.Workflow.Models;

public sealed class WorkflowDefinition
{
    public string Name { get; set; } = string.Empty;

    public IList<IWorkflowStep> Steps { get; set; }
        = new List<IWorkflowStep>();
}