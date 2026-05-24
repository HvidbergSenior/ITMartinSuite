using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioLevelingWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    private readonly ILogger<AudioLevelingWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioLevelingWorkflowStep);

    public AudioLevelingWorkflowStep(
        IAudioEnhancementService audioEnhancementService,
        ILogger<AudioLevelingWorkflowStep> logger)
    {
        _audioEnhancementService =
            audioEnhancementService;

        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video &&
                         !string.IsNullOrWhiteSpace(
                             x.AudioWorkingPath) &&
                         File.Exists(
                             x.AudioWorkingPath) &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            var fileName =
                Path.GetFileName(
                    item.AudioWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var normalizedPath =
                        await _audioEnhancementService
                            .NormalizeAudioAsync(
                                item.AudioWorkingPath!,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Audio leveling progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            normalizedPath))
                    {
                        throw new InvalidOperationException(
                            "Audio leveling returned no output path.");
                    }

                    item.AudioWorkingPath =
                        normalizedPath;
                });
        }
    }
}