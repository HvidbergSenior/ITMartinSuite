using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Runtime.Registry;

public sealed class WorkflowSelector
    : IWorkflowSelector
{
    public WorkflowType? SelectWorkflow(
        MediaFile mediaFile)
    {
        return mediaFile.WorkflowType;
    }
}