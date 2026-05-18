// File: ITMartin.Media.Application/Abstractions/Workflows/IResumableWorkflow.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IResumableWorkflow
{
    Guid WorkflowId { get; }

    string WorkflowName { get; }

    int Version { get; }
}