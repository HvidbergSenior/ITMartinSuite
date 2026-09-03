using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Off by default (see Package4WorkflowState.EnableStabilization) - vidstab's
// 2-pass detect/transform produced badly warped/streaked frames on a vertical
// iPhone clip during manual testing (2026-08-24), even with maxshift/maxangle
// clamps applied. Suspected cause: a rotation-metadata mismatch between the
// decoded frame orientation vidstab assumes and the source's actual
// side-data rotation tag. Implemented properly here (correct 2-pass flow,
// relative working directory to dodge ffmpeg's "C:/..." colon-in-filter-arg
// parsing bug) so it can be re-enabled per-run once that root cause is fixed
// - do not just flip the flag on without re-validating on real footage first.
public sealed class StabilizationWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<StabilizationWorkflowStep> _logger;
    private readonly string _ffmpegPath;

    public override string Name => nameof(StabilizationWorkflowStep);

    public StabilizationWorkflowStep(ILogger<StabilizationWorkflowStep> logger)
    {
        _logger = logger;
        _ffmpegPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
            : "ffmpeg";
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableStabilization)
        {
            _logger.LogInformation("Skipping stabilization (disabled - see class remarks)");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && x.CurrentWorkingPath is not null && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExecuteOperationAsync(item, Name, async () =>
            {
                var workingDir = Path.GetDirectoryName(item.CurrentWorkingPath!)!;
                var sourceFile = Path.GetFileName(item.CurrentWorkingPath!);
                var transformsFile = Path.GetFileNameWithoutExtension(sourceFile) + ".trf";
                var outputFile = Path.GetFileNameWithoutExtension(sourceFile) + ".stabilized.mp4";

                await RunFfmpegAsync(workingDir,
                    $"-y -i \"{sourceFile}\" -vf \"vidstabdetect=shakiness=5:accuracy=15:result={transformsFile}\" -f null -",
                    cancellationToken);

                await RunFfmpegAsync(workingDir,
                    $"-y -i \"{sourceFile}\" -vf \"vidstabtransform=input={transformsFile}:zoom=0:smoothing={state.StabilizationSmoothing}:maxshift=80:maxangle=0.1:crop=black\" -c:v libx264 -preset medium -crf 18 -c:a copy \"{outputFile}\"",
                    cancellationToken);

                item.CurrentWorkingPath = Path.Combine(workingDir, outputFile);
            }, _logger);
        }
    }

    private async Task RunFfmpegAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        // Read stderr concurrently with waiting for exit - ffmpeg writes
        // progress there continuously, and reading it only after exit risks
        // deadlocking on a full pipe buffer for longer clips.
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg (stabilization) failed with exit code {process.ExitCode}: {stderr}");
        }
    }
}
