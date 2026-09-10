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
            () => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? path).AvailableFreeSpace,
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
        CancellationToken cancellationToken)
        where T : struct
    {
        try
        {
            var task = Task.Run(probe, cancellationToken);

            var completed = await Task.WhenAny(
                task,
                Task.Delay(ProbeTimeout, cancellationToken));

            if (completed != task)
            {
                problems.Add(
                    $"{description} did not respond within {ProbeTimeout.TotalSeconds:F0}s - " +
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
