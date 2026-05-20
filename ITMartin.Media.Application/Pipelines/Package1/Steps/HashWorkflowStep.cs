using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class HashWorkflowStep : IWorkflowStep
{
    private readonly ILogger<HashWorkflowStep> _logger;
private readonly IHashService _hashService;
    public HashWorkflowStep(ILogger<HashWorkflowStep> logger, IHashService hashService)
    {
        _logger = logger;
        _hashService = hashService;
    }

    public string Name => "Hashing";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(HashWorkflowStep));
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        foreach (var file in state.MediaFiles)
        {
            if (!string.IsNullOrWhiteSpace(
                    file.Hash))
            {
                continue;
            }

            _logger.LogInformation(
                "Hashing {File}",
                file.FullPath);

            var hash =
                _hashService.ComputeHash(
                    file.FullPath);

            file.SetHash(hash);

            _logger.LogInformation(
                "Hashed {Count}/{Total}",
                state.MediaFiles.Count(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Hash)),
                state.MediaFiles.Count);
        }

        await Task.CompletedTask;
    }
}
