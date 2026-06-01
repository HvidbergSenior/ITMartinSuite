using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class WorkflowRequest
{
    public required Guid MediaFileId { get; init; }

    public required WorkflowType WorkflowType { get; init; }

    public required string FilePath { get; init; }
}