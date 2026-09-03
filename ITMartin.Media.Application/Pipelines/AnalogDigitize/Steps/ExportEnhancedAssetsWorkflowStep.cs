using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class ExportEnhancedAssetsWorkflowStep
    : AnalogDigitizeWorkflowStepBase
{
    private readonly IEnhancedFileNamingService
        _enhancedFileNamingService;

    private readonly ILogger<
            ExportEnhancedAssetsWorkflowStep>
        _logger;

    public override string Name =>
        nameof(ExportEnhancedAssetsWorkflowStep);

    public ExportEnhancedAssetsWorkflowStep(
        IEnhancedFileNamingService enhancedFileNamingService,
        ILogger<ExportEnhancedAssetsWorkflowStep> logger)
    {
        _enhancedFileNamingService =
            enhancedFileNamingService;

        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not AnalogDigitizeWorkflowState state)
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

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.CurrentWorkingPath is not null &&
                    !AlreadyExecuted(x, Name))
                .ToList();

        var total =
            items.Count;

        var current = 0;

        foreach (var item in items)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            current++;

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}",
                Name,
                current,
                total,
                item.CurrentWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var fileName =
                        _enhancedFileNamingService
                            .BuildFileName(item);

                    var sourceLibraryPath =
                        Path.GetDirectoryName(
                            state.WorkingDirectory)!;

                    var relativeDir =
                        Path.GetDirectoryName(
                            Path.GetRelativePath(
                                sourceLibraryPath,
                                item.NormalizedPath))
                        ?? string.Empty;

                    var targetDir =
                        Path.Combine(
                            enhancedDirectory,
                            relativeDir);

                    Directory.CreateDirectory(targetDir);

                    var finalPath =
                        Path.Combine(
                            targetDir,
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

                    await Task.CompletedTask;
                },
                _logger);
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