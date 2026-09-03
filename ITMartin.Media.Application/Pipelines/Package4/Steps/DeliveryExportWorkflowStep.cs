using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Final step - capped bitrate re-encode tuned for social delivery (not the
// reference clip's ~1.9Mbps, which was just TikTok's own re-compression of
// an already-uploaded video, not a creative target - see the DaVinci
// reference-clip analysis this pipeline was designed against). Writes the
// deliverable checkpoint and sets EnhancedOutputPath to it.
public sealed class DeliveryExportWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<DeliveryExportWorkflowStep> _logger;
    private readonly string _ffmpegPath;

    public override string Name => nameof(DeliveryExportWorkflowStep);

    public DeliveryExportWorkflowStep(ILogger<DeliveryExportWorkflowStep> logger)
    {
        _logger = logger;
        _ffmpegPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
            : "ffmpeg";
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;

        var checkpointDirectory = Path.Combine(state.WorkingDirectory, "checkpoints");
        var deliveryDirectory = Path.Combine(state.WorkingDirectory, "delivery");
        Directory.CreateDirectory(checkpointDirectory);
        Directory.CreateDirectory(deliveryDirectory);

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && x.CurrentWorkingPath is not null && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExecuteOperationAsync(item, Name, async () =>
            {
                var workingDir = Path.GetDirectoryName(item.CurrentWorkingPath!)!;
                var sourceFile = Path.GetFileName(item.CurrentWorkingPath!);
                var deliveryFile = Path.GetFileNameWithoutExtension(sourceFile) + ".delivery.mp4";

                var maxRate = $"{state.DeliveryMaxRateMbps}M";
                var bufSize = $"{state.DeliveryMaxRateMbps * 2}M";

                var arguments =
                    $"-y -i \"{sourceFile}\" -c:v libx264 -preset medium -crf {state.DeliveryCrf} " +
                    $"-maxrate {maxRate} -bufsize {bufSize} -c:a aac -b:a {state.DeliveryAudioBitrate} " +
                    $"-movflags +faststart \"{deliveryFile}\"";

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
                    throw new InvalidOperationException($"ffmpeg (delivery export) failed with exit code {process.ExitCode}: {stderr}");
                }

                var deliveryWorkingPath = Path.Combine(workingDir, deliveryFile);
                var finalDeliveryPath = Path.Combine(deliveryDirectory, deliveryFile);
                File.Copy(deliveryWorkingPath, finalDeliveryPath, overwrite: true);

                item.CurrentWorkingPath = deliveryWorkingPath;
                item.EnhancedOutputPath = finalDeliveryPath;

                var checkpointPath = Path.Combine(checkpointDirectory, $"{Path.GetFileNameWithoutExtension(deliveryFile)}.03-final-delivery.mp4");
                File.Copy(finalDeliveryPath, checkpointPath, overwrite: true);
                state.CheckpointPaths.Add(checkpointPath);
            }, _logger);
        }
    }
}
