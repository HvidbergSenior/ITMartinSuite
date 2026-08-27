using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Trim points are explicit (Package4WorkflowState.TrimStartSeconds/
// TrimEndSeconds), not auto-detected - reliably telling "blurry pocket-camera
// opening" apart from "intentionally close framing" needs real content
// analysis this pipeline doesn't have yet. Manual in/out points now, revisit
// automatic dead-footage detection later if it's worth the complexity.
public sealed class TrimDeadFootageWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<TrimDeadFootageWorkflowStep> _logger;
    private readonly string _ffmpegPath;

    public override string Name => nameof(TrimDeadFootageWorkflowStep);

    public TrimDeadFootageWorkflowStep(ILogger<TrimDeadFootageWorkflowStep> logger)
    {
        _logger = logger;
        _ffmpegPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
            : "ffmpeg";
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableTrim || state.TrimStartSeconds <= 0 && state.TrimEndSeconds is null)
        {
            _logger.LogInformation("Skipping trim - disabled or no trim points set");
            return;
        }

        var checkpointDirectory = Path.Combine(state.WorkingDirectory, "checkpoints");
        Directory.CreateDirectory(checkpointDirectory);

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && x.CurrentWorkingPath is not null && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExecuteOperationAsync(item, Name, async () =>
            {
                var workingDir = Path.GetDirectoryName(item.CurrentWorkingPath!)!;
                var sourceFile = Path.GetFileName(item.CurrentWorkingPath!);
                var trimmedFile = Path.GetFileNameWithoutExtension(sourceFile) + ".trimmed.mp4";

                var durationArg = state.TrimEndSeconds.HasValue
                    ? $"-t {(state.TrimEndSeconds.Value - state.TrimStartSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : string.Empty;

                var arguments =
                    $"-y -ss {state.TrimStartSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"-i \"{sourceFile}\" {durationArg} -c:v libx264 -preset medium -crf 18 -c:a aac -b:a 192k \"{trimmedFile}\"";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = arguments,
                        WorkingDirectory = workingDir,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                var stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"ffmpeg (trim) failed with exit code {process.ExitCode}: {stderr}");
                }

                var trimmedPath = Path.Combine(workingDir, trimmedFile);
                item.CurrentWorkingPath = trimmedPath;

                var checkpointPath = Path.Combine(checkpointDirectory, $"{Path.GetFileNameWithoutExtension(trimmedFile)}.02-trimmed.mp4");
                File.Copy(trimmedPath, checkpointPath, overwrite: true);
                state.CheckpointPaths.Add(checkpointPath);
            }, _logger);
        }
    }
}
