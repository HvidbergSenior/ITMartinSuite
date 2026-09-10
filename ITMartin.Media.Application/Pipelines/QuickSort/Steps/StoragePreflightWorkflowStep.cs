using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// Runs FIRST, before any other step touches either path: proves the source and
// the destination are actually there, actually readable/writable, and actually
// have content, and refuses to start the run if not.
//
// WHY THIS STEP EXISTS.
//
// On 2026-09-10 the external drive on the photoserver dropped off the USB bus
// mid-session and came back enumerated as a different device node. The mount
// point stayed in the mount table pointing at the node that no longer existed,
// so the path still "looked" present while every access to it hung and the
// console filled with "Buffer I/O error on dev sda1". That is the third drive
// drop on the same enclosure.
//
// A stale mount is dangerous in two specific ways that this step exists to
// catch, and both of them are silent:
//
//   1. IT HANGS RATHER THAN FAILS. Directory.Exists on a dead fuseblk mount
//      does not return false - it blocks in the kernel, potentially forever.
//      A pipeline that checks a path the obvious way therefore doesn't fail
//      fast, it stops dead with no error, which is exactly what a long
//      unattended run must never do. So every probe below runs with a hard
//      timeout and a timeout is treated as a FAILURE, not as "still thinking".
//
//   2. IT CAN COME BACK EMPTY. If a drive remounts (or a mount point is left
//      unmounted, exposing the empty directory underneath it), the path exists,
//      is readable, is writable - and contains nothing. Without the emptiness
//      check below, the run would scan zero files, export zero files, report
//      success, and quietly deliver an empty library over a real one. Refusing
//      to sort from an unexpectedly empty source is the whole point.
//
// This is a preflight, not a repair: it never mounts, unmounts or otherwise
// touches the system's storage configuration. It only reports precisely what
// is wrong so a human can fix it, because the fix needs privileges the
// pipeline deliberately does not have.
public sealed class StoragePreflightWorkflowStep : QuickSortWorkflowStepBase
{
    // Generous enough that an idle disk spinning up or a NAS waking is not
    // mistaken for a dead one, short enough that an unattended run fails in
    // seconds instead of hanging until someone notices days later.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    // Below this, the export will run out of room partway through and leave a
    // half-written library, which is worse than not starting. Not a guarantee
    // the run fits - just a guard against starting one that obviously cannot.
    private const long MinimumFreeBytes = 5L * 1024 * 1024 * 1024;

    // The output is not smaller than the source. On the 2026-09-10 test run a
    // 13 GB source produced 16 GB of library - the sort removes duplicates and
    // junk, then adds thumbnails, a static gallery and (when enabled) a full
    // baseline mirror of the whole export. Requiring merely "some" free space
    // is therefore useless: what matters is free space measured against the
    // SIZE OF THE SOURCE.
    //
    // 1.2x is the floor, not a forecast. It is what the export alone needed;
    // a run with the baseline snapshot on wants roughly double that. Blocking
    // below the floor catches the case this exists for - a 276 GB source aimed
    // at a 104 GB disk, which would have run for hours before dying.
    private const double MinimumFreeToSourceRatio = 1.2;

    // Measuring 87,000 files takes longer than a liveness probe should, so it
    // gets its own budget. If it cannot finish, the run is NOT blocked - an
    // unmeasurable source is a reason to warn, never a reason to refuse.
    private static readonly TimeSpan SizeProbeTimeout = TimeSpan.FromSeconds(120);

    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<StoragePreflightWorkflowStep> _logger;

    public StoragePreflightWorkflowStep(
        ILibraryPathProvider libraryPathProvider,
        ILogger<StoragePreflightWorkflowStep> logger)
    {
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public override string Name => "StoragePreflight";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var problems = new List<string>();

        // The source. Read-only for us - CLAUDE.md's highest-priority rule is
        // that nothing may ever write to or delete from a source path - so it
        // gets a read probe and an emptiness check, never a write probe.
        if (string.IsNullOrWhiteSpace(state.RootPath))
        {
            problems.Add("No source path was given for this run.");
        }
        else
        {
            problems.AddRange(await CheckReadableAsync(state.RootPath, "Source", cancellationToken));
        }

        // The destination. Same fallback the export step and the add-on chain
        // use, so the preflight checks the path the run will really write to
        // rather than one that only looks like it.
        var outputPath = !string.IsNullOrWhiteSpace(state.OutputPath)
            ? state.OutputPath
            : _libraryPathProvider.LibraryRoot;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            problems.Add("No output path could be resolved for this run.");
        }
        else
        {
            problems.AddRange(
                await CheckRoomForTheSourceAsync(state.RootPath, outputPath, cancellationToken));

            problems.AddRange(await CheckWritableAsync(outputPath, cancellationToken));
        }

        if (problems.Count > 0)
        {
            var message =
                "Storage preflight failed - refusing to start this run:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)) +
                Environment.NewLine +
                "Nothing has been read, written or deleted. Fix the storage and start the run again.";

            _logger.LogError("{Message}", message);

            throw new InvalidOperationException(message);
        }

        _logger.LogInformation(
            "Storage preflight passed: source {Source} is readable and non-empty, output {Output} is writable",
            state.RootPath,
            outputPath);
    }

    // Free space on the filesystem THIS PATH actually lives on.
    //
    // The obvious spelling - DriveInfo(Path.GetPathRoot(path)) - is wrong on
    // Linux, where GetPathRoot returns "/" for every absolute path. It
    // therefore measured the root filesystem no matter what was asked about,
    // and on 2026-09-10 reported 104 GB free for an output path sitting on a
    // freshly mounted 932 GB drive - blocking a run that would have fitted
    // easily. A check that silently measures something other than what it
    // claims is worse than no check.
    //
    // DriveInfo on Unix takes any path and resolves it to the mount point
    // containing it, which is what was wanted all along.
    private static long FreeBytesAt(string path) =>
        new DriveInfo(path).AvailableFreeSpace;

    // Is there room for what this run is about to produce? Free space on its
    // own says nothing - 104 GB sounds generous until the source turns out to
    // be 276 GB, which is exactly the situation this catches.
    //
    // Reports the two numbers in the failure so the reader can see the shape
    // of the problem immediately, rather than a bare "not enough space".
    private async Task<List<string>> CheckRoomForTheSourceAsync(
        string? sourcePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(sourcePath))
            return problems;

        var sourceBytes = await ProbeAsync(
            () => new DirectoryInfo(sourcePath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length),
            $"Source size of {sourcePath}",
            problems,
            cancellationToken,
            SizeProbeTimeout);

        if (sourceBytes is null || sourceBytes.Value <= 0)
        {
            // Could not measure it - that is a warning, not a refusal. The
            // ProbeAsync call above has already recorded why, but this must
            // not be one of the problems that blocks the run, so drop it.
            problems.RemoveAll(p => p.StartsWith($"Source size of {sourcePath}", StringComparison.Ordinal));

            _logger.LogWarning(
                "Could not measure the source size at {Source} - skipping the free-space comparison. " +
                "The run may still fill the destination.",
                sourcePath);

            return problems;
        }

        var freeBytes = await ProbeAsync(
            () => FreeBytesAt(outputPath),
            $"Free space at {outputPath}",
            problems,
            cancellationToken);

        if (freeBytes is null) return problems;

        var needed = (long)(sourceBytes.Value * MinimumFreeToSourceRatio);
        const double gb = 1024.0 * 1024 * 1024;

        _logger.LogInformation(
            "Storage preflight: source {SourceGb:F1} GB, destination {FreeGb:F1} GB free at {Output}",
            sourceBytes.Value / gb, freeBytes.Value / gb, outputPath);

        if (freeBytes.Value < needed)
        {
            problems.Add(
                $"Not enough room at {outputPath}: the source is {sourceBytes.Value / gb:F1} GB " +
                $"but only {freeBytes.Value / gb:F1} GB is free, and a sorted library needs at " +
                $"least {MinimumFreeToSourceRatio:F1}x the source ({needed / gb:F1} GB) - more " +
                "again if the baseline snapshot is on. If the destination is meant to be an " +
                "external drive, check that it is actually mounted: an unmounted mount point is " +
                "an ordinary empty folder on the system disk, and writing there is what fills it.");
        }

        return problems;
    }

    private async Task<List<string>> CheckReadableAsync(
        string path,
        string label,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        var exists = await ProbeAsync(
            () => Directory.Exists(path),
            $"{label} path {path}",
            problems,
            cancellationToken);

        if (exists is null) return problems;      // hung or errored; already reported

        if (!exists.Value)
        {
            problems.Add($"{label} path does not exist: {path}");
            return problems;
        }

        // Enumerating one entry is the cheapest thing that actually touches the
        // filesystem. Directory.Exists can be answered from cached metadata on
        // some mounts, so on its own it is not proof the mount is alive.
        var hasAnyEntry = await ProbeAsync(
            () => Directory.EnumerateFileSystemEntries(path).Any(),
            $"{label} path {path} (listing contents)",
            problems,
            cancellationToken);

        if (hasAnyEntry is null) return problems;

        if (!hasAnyEntry.Value)
        {
            problems.Add(
                $"{label} path is EMPTY: {path}. A drive that dropped and " +
                "remounted, or a mount point left unmounted, looks exactly " +
                "like this. Sorting from it would produce an empty library.");
        }

        return problems;
    }

    private async Task<List<string>> CheckWritableAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        problems.AddRange(await CheckReadableAsync(path, "Output", cancellationToken));

        // An output folder that does not exist yet is normal and fine - the run
        // creates it. An output folder that exists but is empty is also fine,
        // unlike a source. Both of those are reported by CheckReadableAsync as
        // problems, so drop them here and keep only genuine access failures.
        problems.RemoveAll(p =>
            p.Contains("does not exist", StringComparison.Ordinal) ||
            p.Contains("is EMPTY", StringComparison.Ordinal));

        var created = await ProbeAsync(
            () => { Directory.CreateDirectory(path); return true; },
            $"Output path {path} (creating it)",
            problems,
            cancellationToken);

        if (created is null) return problems;

        // The real test. A path can exist and list fine while being read-only
        // (a degraded mount often remounts read-only), and the run would then
        // fail deep inside the export with thousands of files already processed.
        var probeFile = Path.Combine(path, $".preflight-{Guid.NewGuid():N}.tmp");

        var wrote = await ProbeAsync(
            () =>
            {
                File.WriteAllText(probeFile, "preflight");
                var readBack = File.ReadAllText(probeFile);
                File.Delete(probeFile);
                return readBack == "preflight";
            },
            $"Output path {path} (write/read/delete test)",
            problems,
            cancellationToken);

        if (wrote is false)
        {
            problems.Add($"Output path did not return what was written to it: {path}");
        }

        // Best-effort: leaving a stray probe file behind is untidy but harmless,
        // and must never itself fail the preflight.
        try { if (File.Exists(probeFile)) File.Delete(probeFile); } catch { /* ignore */ }

        var freeBytes = await ProbeAsync(
            () => FreeBytesAt(path),
            $"Output path {path} (free space)",
            problems,
            cancellationToken);

        if (freeBytes is not null && freeBytes.Value < MinimumFreeBytes)
        {
            problems.Add(
                $"Output path has only {freeBytes.Value / 1024.0 / 1024 / 1024:F1} GB free " +
                $"(need at least {MinimumFreeBytes / 1024 / 1024 / 1024} GB): {path}");
        }

        return problems;
    }

    // Runs one filesystem probe with a hard timeout, because on a stale mount
    // these calls block in the kernel and never return. Returns null when the
    // probe hung or threw - in both cases a problem has been added to the list
    // and the caller must not draw any conclusion from the path.
    //
    // The abandoned task may stay blocked for as long as the OS takes to give
    // up on the dead device. That is accepted deliberately: one stuck
    // background thread is a far better outcome than a pipeline run that never
    // finishes and reports nothing.
    private async Task<T?> ProbeAsync<T>(
        Func<T> probe,
        string description,
        List<string> problems,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
        where T : struct
    {
        var budget = timeout ?? ProbeTimeout;

        try
        {
            var task = Task.Run(probe, cancellationToken);

            var completed = await Task.WhenAny(
                task,
                Task.Delay(budget, cancellationToken));

            if (completed != task)
            {
                problems.Add(
                    $"{description} did not respond within {budget.TotalSeconds:F0}s - " +
                    "this is what a disconnected drive or a stale mount looks like. " +
                    "Check that the device is still attached and that the mount " +
                    "points at the device it currently is (a drive that drops off " +
                    "the bus can come back under a different device node).");

                return null;
            }

            return await task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            problems.Add($"{description} failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
