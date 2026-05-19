using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class FileDiscoveryWorkflowStep
    : IWorkflowStep
{
    private readonly IFileScanner _fileScanner;
    private readonly IMediaTypeResolver
        _mediaTypeResolver;
    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner, IMediaTypeResolver mediaTypeResolver)
    {
        _fileScanner = fileScanner;
        _mediaTypeResolver = mediaTypeResolver;
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

        if (state.MediaFiles.Count > 0)
        {
            return;
        }

        var files =
            await _fileScanner.ScanAsync(
                state.RootPath,
                cancellationToken);

        state.MediaFiles =
            files
                .Select(path =>
                    new MediaFile(
                        path,
                        File.GetCreationTimeUtc(path),
                        _mediaTypeResolver.Resolve(path),
                        new FileInfo(path).Length))
                .ToList();
    }
}