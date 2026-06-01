namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class WorkflowAttachment
{
    public required Guid MediaFileId { get; init; }

    public required MediaFile MediaFile { get; init; }
}