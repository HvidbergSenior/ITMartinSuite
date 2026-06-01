using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class MediaWorkflowRequest
{
    public Guid WorkflowId { get; init; }

    public Guid MediaFileId { get; init; }

    public WorkflowType WorkflowType { get; init; }

    public string WorkflowName { get; init; } = null!;

    public string FilePath { get; init; } = null!;
}