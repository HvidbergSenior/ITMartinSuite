using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Application.Workflows.Steps;

public sealed class CrashWorkflowStep
    : IWorkflowStep
{
    public string Name => "Crash";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        throw new Exception("Simulated crash");
    }
}