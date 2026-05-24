using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioSpeechEnhancementWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    private readonly ILogger<
            AudioSpeechEnhancementWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioSpeechEnhancementWorkflowStep);

    public AudioSpeechEnhancementWorkflowStep(
        IAudioEnhancementService audioEnhancementService,
        ILogger<AudioSpeechEnhancementWorkflowStep> logger)
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
                         x.AudioWorkingPath is not null &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var fileName =
                        Path.GetFileName(
                            item.AudioWorkingPath);

                    _logger.LogInformation(
                        "Starting audio speech enhancement for {File}",
                        fileName);

                    var enhancedSpeechPath =
                        await _audioEnhancementService
                            .EnhanceSpeechAsync(
                                item.AudioWorkingPath!,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Audio speech enhancement progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            enhancedSpeechPath))
                    {
                        throw new InvalidOperationException(
                            "Audio speech enhancement returned no output path.");
                    }

                    item.AudioWorkingPath =
                        enhancedSpeechPath;

                    _logger.LogInformation(
                        "Completed audio speech enhancement for {File}",
                        fileName);
                });
        }
    }
}