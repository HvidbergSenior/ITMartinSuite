using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioNoiseReductionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioEnhancementService
        _audioEnhancementService;

    private readonly ILogger<
            AudioNoiseReductionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioNoiseReductionWorkflowStep);

    public AudioNoiseReductionWorkflowStep(
        IAudioEnhancementService audioEnhancementService,
        ILogger<AudioNoiseReductionWorkflowStep> logger)
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
                        "Starting audio noise reduction for {File}",
                        fileName);

                    var reducedNoisePath =
                        await _audioEnhancementService
                            .ReduceNoiseAsync(
                                item.AudioWorkingPath!,
                                state.RestorationProfile,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Audio noise reduction progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            reducedNoisePath))
                    {
                        throw new InvalidOperationException(
                            "Audio noise reduction returned no output path.");
                    }

                    item.AudioWorkingPath =
                        reducedNoisePath;

                    _logger.LogInformation(
                        "Completed audio noise reduction for {File}",
                        fileName);
                });
        }
    }
}