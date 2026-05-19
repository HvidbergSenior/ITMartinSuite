using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
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

        foreach (var file in state.Files)
        {
            if (state.HashedFiles.Contains(file))
            {
                continue;
            }

            _logger.LogInformation(
                "Hashing {File}",
                file);

            // TODO:
            // real hash logic later

            state.HashedFiles.Add(file);

            _logger.LogInformation(
                "Hashed {Count}/{Total}",
                state.HashedFiles.Count,
                state.Files.Count);

            // TEMP crash test
            // throw new Exception("Crash test");
        }

        await _processor.ProcessAsync(
            cancellationToken);
    }
}
