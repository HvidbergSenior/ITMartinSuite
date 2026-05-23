using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ExportEnhancedAssetsWorkflowStep
    : IWorkflowStep
{
    private readonly IEnhancedFileNamingService
        _enhancedFileNamingService;

    public string Name =>
        nameof(ExportEnhancedAssetsWorkflowStep);

    public ExportEnhancedAssetsWorkflowStep(
        IEnhancedFileNamingService enhancedFileNamingService)
    {
        _enhancedFileNamingService =
            enhancedFileNamingService;
    }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        var enhancedDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "enhanced");

        var manifestDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "manifests");

        Directory.CreateDirectory(
            enhancedDirectory);

        Directory.CreateDirectory(
            manifestDirectory);

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.CurrentWorkingPath is not null &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var fileName =
                _enhancedFileNamingService
                    .BuildFileName(item);

            var finalPath =
                Path.Combine(
                    enhancedDirectory,
                    fileName);

            File.Copy(
                item.CurrentWorkingPath!,
                finalPath,
                overwrite: true);

            CopyDates(
                item.NormalizedPath,
                finalPath);

            item.CurrentWorkingPath =
                finalPath;

            item.EnhancedOutputPath =
                finalPath;

            item.Operations.Add(
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt =
                        DateTimeOffset.UtcNow,

                    CompletedAt =
                        DateTimeOffset.UtcNow,

                    Success = true,

                    Metadata = finalPath
                });
        }

        var manifestPath =
            Path.Combine(
                manifestDirectory,
                "package2-manifest.json");

        var manifestJson =
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            manifestPath,
            manifestJson,
            cancellationToken);
    }

    private static void CopyDates(
        string sourcePath,
        string destinationPath)
    {
        var created =
            File.GetCreationTime(
                sourcePath);

        var modified =
            File.GetLastWriteTime(
                sourcePath);

        File.SetCreationTime(
            destinationPath,
            created);

        File.SetLastWriteTime(
            destinationPath,
            modified);
    }
}