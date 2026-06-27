using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

/// <summary>
/// Runs before FileDiscovery. Detects VIDEO_TS / VIDEO_RM DVD folder structures,
/// concatenates the content VOBs into a single MP4 (stream copy — no re-encode),
/// then moves the source DVD folder to .dvd-source/ so FileScanner skips it.
/// </summary>
public sealed class DvdJoinWorkflowStep : Package1WorkflowStepBase
{
    private readonly ILogger<DvdJoinWorkflowStep> _logger;

    private static readonly string[] DvdFolderNames = ["VIDEO_TS", "VIDEO_RM"];

    public DvdJoinWorkflowStep(ILogger<DvdJoinWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "DvdJoin";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var dvdFolders = Directory
            .EnumerateDirectories(state.RootPath)
            .Where(d => DvdFolderNames.Contains(
                Path.GetFileName(d),
                StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (dvdFolders.Count == 0)
        {
            _logger.LogInformation("No DVD folders (VIDEO_TS/VIDEO_RM) found — skipping DvdJoin");
            return;
        }

        foreach (var folder in dvdFolders)
        {
            await ExecuteOperationAsync(
                "JoinDvdVobs",
                Path.GetFileName(folder),
                () => JoinAsync(folder, state.RootPath, cancellationToken),
                _logger);
        }
    }

    private async Task JoinAsync(string dvdFolder, string rootPath, CancellationToken cancellationToken)
    {
        var folderName = Path.GetFileName(dvdFolder)!.ToUpperInvariant();

        // Content VOBs are VTS_XX_N.VOB where N >= 1 (_0 = navigation menu, skip it)
        var vobs = Directory
            .EnumerateFiles(dvdFolder, "*.vob", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(dvdFolder, "*.VOB", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsContentVob)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (vobs.Count == 0)
        {
            _logger.LogInformation("{Folder}: no content VOBs found", folderName);
            ArchiveDvdFolder(dvdFolder, rootPath);
            return;
        }

        _logger.LogInformation("{Folder}: joining {Count} VOB(s): {Files}",
            folderName,
            vobs.Count,
            string.Join(", ", vobs.Select(Path.GetFileName)));

        var outputPath = Path.Combine(rootPath, $"{folderName}.mp4");
        var concatFile = Path.Combine(Path.GetTempPath(), $"dvd_concat_{Guid.NewGuid():N}.txt");

        try
        {
            // ffmpeg concat demuxer requires one "file 'path'" line per input
            await File.WriteAllLinesAsync(
                concatFile,
                vobs.Select(v => $"file '{v.Replace("\\", "/")}'"),
                cancellationToken);

            await RunFfmpegAsync(
                $"-y -f concat -safe 0 -i \"{concatFile}\" -c copy \"{outputPath}\"",
                cancellationToken);
        }
        finally
        {
            if (File.Exists(concatFile)) File.Delete(concatFile);
        }

        _logger.LogInformation("{Folder}: joined → {Output}", folderName, outputPath);
        ArchiveDvdFolder(dvdFolder, rootPath);
    }

    private static bool IsContentVob(string path)
    {
        // Accept VTS_XX_N.VOB where N (the segment index) is >= 1
        var name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        var parts = name.Split('_');
        return parts.Length == 3 &&
               parts[0] == "VTS" &&
               int.TryParse(parts[2], out var n) && n >= 1;
    }

    private void ArchiveDvdFolder(string dvdFolder, string rootPath)
    {
        // Move to .dvd-source/ — FileScanner skips folders starting with '.'
        var archiveRoot = Path.Combine(rootPath, ".dvd-source");
        Directory.CreateDirectory(archiveRoot);
        var dest = Path.Combine(archiveRoot, Path.GetFileName(dvdFolder)!);
        if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
        Directory.Move(dvdFolder, dest);
        _logger.LogInformation("Archived {Folder} → .dvd-source/", Path.GetFileName(dvdFolder));
    }

    private async Task RunFfmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        var ffmpegPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
            : "ffmpeg";

        _logger.LogInformation("ffmpeg {Arguments}", arguments);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Must drain both streams to avoid deadlock
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg exited with code {process.ExitCode}: {stderr[..Math.Min(500, stderr.Length)]}");
    }
}
