using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowFactory
{
    IWorkflowDefinition GetWorkflow(
        WorkflowType workflowType);
}