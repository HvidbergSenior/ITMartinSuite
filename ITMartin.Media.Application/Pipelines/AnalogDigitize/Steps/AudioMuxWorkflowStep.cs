using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class AudioMuxWorkflowStep
    : AnalogDigitizeWorkflowStepBase
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

        if (!state.EnableAudioEnhancement)
        {
            _logger.LogInformation(
                "Skipping audio mux because audio enhancement is disabled.");

            return;
        }

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.MediaKind == MediaKind.Video &&
                    x.CurrentWorkingPath is not null &&
                    !string.IsNullOrWhiteSpace(
                        x.AudioWorkingPath) &&
                    File.Exists(
                        x.AudioWorkingPath) &&
                    !AlreadyExecuted(
                        x,
                        Name))
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
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var fileName =
                        Path.GetFileName(
                            item.CurrentWorkingPath);

                    _logger.LogInformation(
                        "Starting audio mux for {File}",
                        fileName);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(10));

                    var muxedPath =
                        await _audioExtractionService
                            .MuxAsync(
                                item.CurrentWorkingPath!,
                                item.AudioWorkingPath!,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Audio mux progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cts.Token);

                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(
                            muxedPath))
                    {
                        throw new InvalidOperationException(
                            "Audio mux returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        muxedPath;

                    _logger.LogInformation(
                        """
                        Audio mux completed
                        File: {File}
                        Output: {Output}
                        """,
                        fileName,
                        muxedPath);
                },
                _logger);
        }
    }
}