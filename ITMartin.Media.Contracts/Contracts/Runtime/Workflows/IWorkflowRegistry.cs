namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowRegistry
{
    IWorkflowDefinition Resolve(string workflowName);
}