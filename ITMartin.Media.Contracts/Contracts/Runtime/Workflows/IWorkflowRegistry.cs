namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowRegistry
{
    IWorkflowDefinition Resolve(string workflowName);
}