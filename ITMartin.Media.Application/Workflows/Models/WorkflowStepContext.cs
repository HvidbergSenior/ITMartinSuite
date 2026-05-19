namespace ITMartin.Media.Application.Workflow.Models;

public sealed class WorkflowStepContext
{
    public Guid SessionId { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public Dictionary<string, object> Items { get; set; } = new();

    public CancellationToken CancellationToken { get; set; }
}