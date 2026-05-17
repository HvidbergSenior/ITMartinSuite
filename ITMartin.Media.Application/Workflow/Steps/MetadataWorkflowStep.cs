using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Models;

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
        WorkflowStepContext context)
    {
        await _processor.ProcessAsync(
            context.CancellationToken);
    }
}