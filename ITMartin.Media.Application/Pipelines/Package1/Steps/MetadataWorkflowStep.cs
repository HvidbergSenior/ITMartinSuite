using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class MetadataWorkflowStep : IWorkflowStep
{
    private readonly MetadataProcessor _processor;
private readonly ILogger<MetadataWorkflowStep> _logger;
    public MetadataWorkflowStep(
        MetadataProcessor processor, ILogger<MetadataWorkflowStep> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string Name => "Metadata";
    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
            var state =
                context.State as Package1WorkflowState
                ?? throw new InvalidOperationException(
                    "Invalid workflow state");
            cancellationToken.ThrowIfCancellationRequested();

            var files = state.Files;
            state.MetadataFiles = files.ToList();
            
            await _processor.ProcessAsync(
                cancellationToken);
            
    }
}