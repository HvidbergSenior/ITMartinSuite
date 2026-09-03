using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Stand-in for a real custom LUT (a proper .cube grade file, imported via
// ffmpeg's lut3d filter, is the real "one consistent look across every clip"
// tool) - no LUT file exists yet, so this uses a built-in curves preset as an
// approximation. Swap in lut3d=file.cube once a real grade is designed.
public sealed class ColorGradeWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<ColorGradeWorkflowStep> _logger;
    public override string Name => nameof(ColorGradeWorkflowStep);

    public ColorGradeWorkflowStep(ILogger<ColorGradeWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableColorGrade)
        {
            _logger.LogInformation("Skipping color grade");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("curves=preset=medium_contrast");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
