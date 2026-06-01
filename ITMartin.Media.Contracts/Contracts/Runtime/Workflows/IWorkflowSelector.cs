using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowSelector
{
    WorkflowType? SelectWorkflow(
        MediaFile mediaFile);
}