using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AudioMuxWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IAudioExtractionService
        _audioExtractionService;

    private readonly ILogger<
            AudioMuxWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AudioMuxWorkflowStep);

    public AudioMuxWorkflowStep(
        IAudioExtractionService audioExtractionService,
        ILogger<AudioMuxWorkflowStep> logger)
    {
        _audioExtractionService =
            audioExtractionService;

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
                         x.CurrentWorkingPath is not null &&
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
                    _logger.LogInformation(
                        "START AudioMux {Video}",
                        item.CurrentWorkingPath);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(10));

                    item.CurrentWorkingPath =
                        await _audioExtractionService
                            .MuxAsync(
                                item.CurrentWorkingPath!,
                                item.AudioWorkingPath!,
                                cts.Token);

                    _logger.LogInformation(
                        "END AudioMux {Video}",
                        item.CurrentWorkingPath);
                });
        }
    }
}