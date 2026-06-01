namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public static class WorkflowJobTypes
{
    public const string StartWorkflow =
        nameof(StartWorkflow);

    public const string ResumeWorkflow =
        nameof(ResumeWorkflow);

    public const string RecoverWorkflow =
        nameof(RecoverWorkflow);
}