using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class TestStep2
    : IWorkflowStep
{
    public string Name =>
        "Step2";

    public Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("STEP 2");

        return Task.CompletedTask;
    }
}