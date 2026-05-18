using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Domain.Interfaces;

namespace ITMartin.Media.Application.Workflow.Steps;

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

    public async Task ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var rootPath =
            context.Items["RootPath"]?.ToString()
            ?? throw new InvalidOperationException(
                "RootPath missing");

        var files =
            await _fileScanner.ScanAsync(
                rootPath,
                cancellationToken);

        context.Items["files"] = files;
    }
}