using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

// Byte-hash dedup (DuplicateDetectionWorkflowStep) only catches identical
// files, but the same song synced from multiple phones/backups is rarely
// byte-identical (different rip, different bitrate, re-tagged copy) even
// though it's clearly the same track. This runs after tags are extracted
// and groups by (Artist, Title) instead, keeping the largest file (a simple
// proxy for bitrate/quality) per group and routing the rest to Duplicates -
// same convention the hash pass already uses.
public sealed class AudioDuplicateDetectionWorkflowStep : Package1WorkflowStepBase
{
    private readonly ILogger<AudioDuplicateDetectionWorkflowStep> _logger;

    public AudioDuplicateDetectionWorkflowStep(ILogger<AudioDuplicateDetectionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "AudioDuplicateDetection";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        await ExecuteOperationAsync(
            "AudioDuplicateDetection",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var duplicateCount = 0;

                var groups = state.MediaFiles
                    .Where(f =>
                        f.Type == MediaType.Audio &&
                        f.ExportSubFolder != "Duplicates" &&
                        !string.IsNullOrWhiteSpace(f.Artist) &&
                        !string.IsNullOrWhiteSpace(f.Title))
                    .GroupBy(f => (
                        Artist: f.Artist!.Trim().ToLowerInvariant(),
                        Title: f.Title!.Trim().ToLowerInvariant()));

                foreach (var group in groups)
                {
                    var tracks = group.ToList();
                    if (tracks.Count < 2) continue;

                    // Largest file wins (best proxy for bitrate/quality available
                    // without decoding audio); the rest are near-duplicates.
                    var keeper = tracks.OrderByDescending(t => t.SizeBytes).First();

                    foreach (var track in tracks.Where(t => t != keeper))
                    {
                        track.ExportSubFolder = "Duplicates";
                        duplicateCount++;
                    }
                }

                _logger.LogInformation(
                    "Audio near-duplicates (same Artist/Title, different file): {Count}",
                    duplicateCount);

                await Task.CompletedTask;
            },
            _logger);
    }
}
