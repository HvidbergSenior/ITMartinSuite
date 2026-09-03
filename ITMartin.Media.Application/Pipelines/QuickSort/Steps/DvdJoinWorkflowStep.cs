using System.Diagnostics;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

/// <summary>
/// Runs before FileDiscovery. Recursively locates DVD content folders
/// (any folder directly containing VTS_XX_N.VOB files where N >= 1),
/// concatenates them into a single MP4 per disc (stream copy — no re-encode),
/// then archives the source files to .dvd-source/ so FileScanner skips them.
///
/// Handles both layouts:
///   - Subfolder layout:  DVD-1/VIDEO_TS/VTS_01_1.VOB  → DVD-1_VIDEO_TS.mp4
///   - Flat layout:       DVD-2/VTS_01_1.VOB            → DVD-2.mp4
/// </summary>
public sealed class DvdJoinWorkflowStep : QuickSortWorkflowStepBase
{
    private readonly ILogger<DvdJoinWorkflowStep> _logger;

    public DvdJoinWorkflowStep(ILogger<DvdJoinWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "DvdJoin";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var dvdFolders = FindDvdContentFolders(state.RootPath, maxDepth: 3).ToList();

        if (dvdFolders.Count == 0)
        {
            _logger.LogInformation("No DVD content folders found — skipping DvdJoin");
            return;
        }

        _logger.LogInformation("Found {Count} DVD content folder(s): {Folders}",
            dvdFolders.Count,
            string.Join(", ", dvdFolders.Select(f => RelativeName(f, state.RootPath))));

        foreach (var folder in dvdFolders)
        {
            await ExecuteOperationAsync(
                "JoinDvdVobs",
                RelativeName(folder, state.RootPath),
                () => JoinAsync(folder, state.RootPath, cancellationToken),
                _logger);
        }
    }

    // Recursively finds folders that directly contain content VOBs (VTS_XX_N where N>=1).
    // Stops recursing into a folder once content VOBs are found there.
    private static IEnumerable<string> FindDvdContentFolders(string directory, int maxDepth)
    {
        if (!Directory.Exists(directory) || maxDepth < 0) yield break;

        var name = Path.GetFileName(directory);
        if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#') ||
            name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
            yield break;

        var hasContent = Directory
            .EnumerateFiles(directory, "*.vob", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(directory, "*.VOB", SearchOption.TopDirectoryOnly))
            .Any(IsContentVob);

        if (hasContent)
        {
            yield return directory;
            yield break; // don't recurse into a DVD content folder
        }

        foreach (var sub in Directory.EnumerateDirectories(directory))
        {
            foreach (var found in FindDvdContentFolders(sub, maxDepth - 1))
                yield return found;
        }
    }

    private async Task JoinAsync(string dvdFolder, string rootPath, CancellationToken cancellationToken)
    {
        var vobs = Directory
            .EnumerateFiles(dvdFolder, "*.vob", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(dvdFolder, "*.VOB", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsContentVob)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (vobs.Count == 0)
        {
            _logger.LogInformation("{Folder}: no content VOBs — archiving only", RelativeName(dvdFolder, rootPath));
            Archive(dvdFolder, rootPath);
            return;
        }

        _logger.LogInformation("{Folder}: joining {Count} VOB(s): {Files}",
            RelativeName(dvdFolder, rootPath),
            vobs.Count,
            string.Join(", ", vobs.Select(Path.GetFileName)));

        // Output MP4 goes into rootPath, named after relative path with _ separators
        var outputName = RelativeName(dvdFolder, rootPath).Replace(Path.DirectorySeparatorChar, '_').Replace('/', '_');
        var outputPath = Path.Combine(rootPath, outputName + ".mp4");

        var concatFile = Path.Combine(Path.GetTempPath(), $"dvd_{Guid.NewGuid():N}.txt");
        try
        {
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

        _logger.LogInformation("{Folder}: joined → {Output}", RelativeName(dvdFolder, rootPath), outputPath);
        Archive(dvdFolder, rootPath);
    }

    // VTS_XX_N.VOB where N >= 1 (skip _0 = navigation menu)
    private static bool IsContentVob(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        var parts = name.Split('_');
        return parts.Length == 3 &&
               parts[0] == "VTS" &&
               int.TryParse(parts[2], out var n) && n >= 1;
    }

    private void Archive(string dvdFolder, string rootPath)
    {
        var archiveRoot = Path.Combine(rootPath, ".dvd-source");
        Directory.CreateDirectory(archiveRoot);

        var isRoot = dvdFolder.TrimEnd(Path.DirectorySeparatorChar, '/')
            .Equals(rootPath.TrimEnd(Path.DirectorySeparatorChar, '/'),
                StringComparison.OrdinalIgnoreCase);

        if (isRoot)
        {
            // Can't move the root itself — move the DVD files individually
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".vob", ".ifo", ".bup", ".dat" };
            foreach (var f in Directory.EnumerateFiles(dvdFolder)
                         .Where(f => extensions.Contains(Path.GetExtension(f))))
            {
                var dest = Path.Combine(archiveRoot, Path.GetFileName(f));
                File.Move(f, dest, overwrite: true);
            }
        }
        else
        {
            var dest = Path.Combine(archiveRoot, Path.GetFileName(dvdFolder)!);
            if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
            Directory.Move(dvdFolder, dest);
        }

        _logger.LogInformation("Archived {Folder} → .dvd-source/", RelativeName(dvdFolder, rootPath));
    }

    private static string RelativeName(string folder, string rootPath)
    {
        var rel = Path.GetRelativePath(rootPath, folder);
        return rel == "." ? Path.GetFileName(folder) ?? folder : rel;
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
