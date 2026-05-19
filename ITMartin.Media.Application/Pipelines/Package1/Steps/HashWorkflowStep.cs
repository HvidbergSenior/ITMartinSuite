using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Runtime.Interfaces;
using ITMartin.Media.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class HashWorkflowStep : IWorkflowStep
{
    private readonly HashProcessor _processor;
private readonly ILogger<HashWorkflowStep> _logger;
    public HashWorkflowStep(
        HashProcessor processor, ILogger<HashWorkflowStep> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string Name => "Hashing";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
            var state =
                context.State as Package1WorkflowState
                ?? throw new InvalidOperationException(
                    "Invalid workflow state");
            
            var files = state.Files;
            
            state.HashedFiles = files.ToList();
            
            await _processor.ProcessAsync(
                cancellationToken);
        }
    }
