using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests;

public sealed class StartWorkflowRequest
{
    public Guid WorkflowId { get; init; }

    public WorkflowType WorkflowType { get; init; }

    public string FilePath { get; init; } = null!;
}