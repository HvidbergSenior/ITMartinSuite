using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class TestWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "TestWorkflow";

    public IReadOnlyCollection<IWorkflowStep> Steps =>
    [
        new TestStep1(),
        new TestStep2()
    ];
}