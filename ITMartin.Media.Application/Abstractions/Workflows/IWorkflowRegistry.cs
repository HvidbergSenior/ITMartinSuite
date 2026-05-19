// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowRegistry.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowRegistry
{
    IWorkflowDefinition Resolve(string workflowName);
}