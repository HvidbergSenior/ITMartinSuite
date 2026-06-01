namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowRegistry
{

    IWorkflowDefinition Resolve(
        WorkflowType workflowType);
}