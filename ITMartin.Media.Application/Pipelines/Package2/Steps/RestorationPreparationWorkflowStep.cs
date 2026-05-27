using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class RestorationPreparationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            RestorationPreparationWorkflowStep>
        _logger;

    public override string Name =>
        nameof(RestorationPreparationWorkflowStep);

    public RestorationPreparationWorkflowStep(
        ILogger<RestorationPreparationWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            throw new InvalidOperationException(
                "Invalid workflow state.");
        }

        Directory.CreateDirectory(
            state.WorkingDirectory);

        var workingDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "working");

        var enhancedDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "enhanced");

        var thumbnailDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "thumbnails");

        var manifestDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "manifests");

        var tempDirectory =
            Path.Combine(
                state.WorkingDirectory,
                "temp");

        Directory.CreateDirectory(
            workingDirectory);

        Directory.CreateDirectory(
            enhancedDirectory);

        Directory.CreateDirectory(
            thumbnailDirectory);

        Directory.CreateDirectory(
            manifestDirectory);

        Directory.CreateDirectory(
            tempDirectory);

        var total =
            state.Items.Count;

        var current = 0;

        foreach (var item in state.Items)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            current++;

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}",
                Name,
                current,
                total,
                item.NormalizedPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    if (!File.Exists(
                            item.NormalizedPath))
                    {
                        throw new InvalidOperationException(
                            "Normalized file does not exist.");
                    }

                    var extension =
                        Path.GetExtension(
                                item.NormalizedPath)
                            .ToLowerInvariant();

                    if (extension is not ".jpg"
                        && extension is not ".mp4")
                    {
                        throw new InvalidOperationException(
                            "Unsupported normalized format.");
                    }

                    var fileName =
                        Path.GetFileName(
                            item.NormalizedPath);

                    var workingPath =
                        Path.Combine(
                            workingDirectory,
                            fileName);

                    var enhancedOutputPath =
                        Path.Combine(
                            enhancedDirectory,
                            fileName);

                    var thumbnailOutputPath =
                        Path.Combine(
                            thumbnailDirectory,
                            $"{Path.GetFileNameWithoutExtension(fileName)}.jpg");

                    File.Copy(
                        item.NormalizedPath,
                        workingPath,
                        overwrite: true);

                    item.CurrentWorkingPath =
                        workingPath;

                    item.EnhancedOutputPath =
                        enhancedOutputPath;

                    item.ThumbnailOutputPath =
                        thumbnailOutputPath;

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}