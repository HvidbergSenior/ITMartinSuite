using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Workflows.Steps;

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