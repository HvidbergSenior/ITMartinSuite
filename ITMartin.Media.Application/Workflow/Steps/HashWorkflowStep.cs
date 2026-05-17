using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Models;
using ITMartin.Media.Application.Services;

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
        WorkflowStepContext context)
    {
        await _processor.ProcessAsync(
            context.CancellationToken);
    }
}