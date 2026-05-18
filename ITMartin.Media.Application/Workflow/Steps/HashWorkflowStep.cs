using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Processors;

namespace ITMartin.Media.Application.Workflow.Steps;

public sealed class HashWorkflowStep : IWorkflowStep
{
    private readonly HashProcessor _processor;

    public HashWorkflowStep(
        HashProcessor processor)
    {
        _processor = processor;
    }

    public string Name => "Hashing";

    public async Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files =
                context.Items["files"];

            await _processor.ProcessAsync(
                cancellationToken);

            context.Items["hashedFiles"] =
                files;
        }
    }
}