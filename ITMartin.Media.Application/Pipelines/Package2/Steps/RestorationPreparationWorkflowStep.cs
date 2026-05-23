using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class RestorationPreparationWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(RestorationPreparationWorkflowStep);

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
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

        foreach (var item in state.Items)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!File.Exists(
                    item.NormalizedPath))
            {
                item.Failed = true;

                item.FailureReason =
                    "Normalized file does not exist.";

                continue;
            }

            var extension =
                Path.GetExtension(
                        item.NormalizedPath)
                    .ToLowerInvariant();

            if (extension is not ".jpg"
                && extension is not ".mp4")
            {
                item.Failed = true;

                item.FailureReason =
                    "Unsupported normalized format.";

                continue;
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

            item.Operations.Add(
                new EnhancementOperation
                {
                    Name = Name,
                    StartedAt =
                        DateTimeOffset.UtcNow,

                    CompletedAt =
                        DateTimeOffset.UtcNow,

                    Success = true
                });
        }

        await Task.CompletedTask;
    }
}