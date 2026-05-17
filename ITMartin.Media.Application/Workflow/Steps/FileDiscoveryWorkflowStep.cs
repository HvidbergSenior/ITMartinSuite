using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Models;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Workflow.Steps;

public sealed class FileDiscoveryWorkflowStep : IWorkflowStep
{
    private readonly IFileScanner _fileScanner;

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner)
    {
        _fileScanner = fileScanner;
    }

    public string Name => "FileDiscovery";

    public async Task ExecuteAsync(
        WorkflowStepContext context)
    {
        var files = await _fileScanner.ScanAsync(
            context.RootPath,
            context.CancellationToken);

        context.Items["files"] = files;
    }
}