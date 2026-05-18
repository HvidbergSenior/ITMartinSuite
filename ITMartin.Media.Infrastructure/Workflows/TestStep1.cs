using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class TestStep1
    : IWorkflowStep
{
    public string Name =>
        "Step1";

    public Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("STEP 1");

        return Task.CompletedTask;
    }
}