using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// All prior steps only registered filter strings (Package2's proven pattern -
// one combined ffmpeg pass beats N separate re-encodes). This is that single
// render, and its output is the first real checkpoint file: "graded and
// audio-cleaned, before trim/delivery compression."
public sealed class VideoAudioRenderWorkflowStep : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService _videoEnhancementService;
    private readonly ILogger<VideoAudioRenderWorkflowStep> _logger;

    public override string Name => nameof(VideoAudioRenderWorkflowStep);

    public VideoAudioRenderWorkflowStep(IVideoEnhancementService videoEnhancementService, ILogger<VideoAudioRenderWorkflowStep> logger)
    {
        _videoEnhancementService = videoEnhancementService;
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;

        var items = state.Items
            .Where(x => !x.Failed && x.MediaKind == MediaKind.Video && x.CurrentWorkingPath is not null
                        && (x.VideoFilters.Count > 0 || x.AudioFilters.Count > 0) && !AlreadyExecuted(x, Name))
            .ToList();

        if (items.Count == 0)
        {
            _logger.LogInformation("Skipping combined render - no filters registered");
            return;
        }

        var checkpointDirectory = Path.Combine(state.WorkingDirectory, "checkpoints");
        Directory.CreateDirectory(checkpointDirectory);

        var total = items.Count;
        var current = 0;

        foreach (var item in items)
        {
            current++;
            cancellationToken.ThrowIfCancellationRequested();

            var videoFilterChain = string.Join(",", item.VideoFilters);
            var audioFilterChain = string.Join(",", item.AudioFilters);

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}\nVideo: {Video}\nAudio: {Audio}",
                Name, current, total, item.CurrentWorkingPath, videoFilterChain, audioFilterChain);

            await ExecuteOperationAsync(item, Name, async () =>
            {
                var renderedPath = await _videoEnhancementService.ApplyFiltersAsync(
                    item.CurrentWorkingPath!,
                    videoFilterChain,
                    audioFilterChain,
                    progressValue => _logger.LogInformation("Render progress {File}: {Progress:P0}", item.CurrentWorkingPath, progressValue),
                    cancellationToken,
                    crf: 18,
                    preset: "medium",
                    codec: "libx264");

                if (string.IsNullOrWhiteSpace(renderedPath))
                {
                    throw new InvalidOperationException("Combined render returned no output path.");
                }

                item.CurrentWorkingPath = renderedPath;

                var checkpointPath = Path.Combine(checkpointDirectory, $"{Path.GetFileNameWithoutExtension(renderedPath)}.01-graded.mp4");
                File.Copy(renderedPath, checkpointPath, overwrite: true);
                state.CheckpointPaths.Add(checkpointPath);
            }, _logger);
        }
    }
}
