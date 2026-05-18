
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Processors;

namespace ITMartin.Media.Application.Workflow.Steps;

public sealed class MetadataWorkflowStep : IWorkflowStep
{
    private readonly MetadataProcessor _processor;

    public MetadataWorkflowStep(
        MetadataProcessor processor)
    {
        _processor = processor;
    }

    public string Name => "Metadata";
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

            context.Items["metadataFiles"] =
                files;
        }
    }
}