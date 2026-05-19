using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Runtime.Interfaces;
using ITMartin.Media.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class FileDiscoveryWorkflowStep
    : IWorkflowStep
{
    private readonly IFileScanner _fileScanner;

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner)
    {
        _fileScanner = fileScanner;
    }

    public string Name => "FileDiscovery";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var files =
            await _fileScanner.ScanAsync(
                state.RootPath,
                cancellationToken);

        state.Files =
            files.ToList();
    }
}