namespace ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

/// <summary>
/// A library's ".baseline" copy is a full, untouched mirror of exactly what
/// QuickSort's own export produced - taken before any FaceIndex/add-on step
/// (free or paid) has had a chance to move, delete, or overwrite anything.
/// Some add-ons are destructive in ways a re-run can't undo (FixOrientationAsync
/// overwrites the image file directly, LibraryPolishService deletes/moves
/// files) - the baseline exists so add-ons can always be experimented with,
/// or safely re-run, by restoring back to this exact starting point first.
///
/// Stored as a sibling of the library folder (never nested inside it) so it
/// never needs to be added to every "skip this folder" list already scattered
/// across gallery browsing, face indexing, and cleanup - nothing that scans
/// the real library ever walks into a directory it doesn't already know about.
/// </summary>
public static class QuickSortBaselineHelper
{
    public static string GetBaselinePath(string outputPath)
    {
        var trimmed = outputPath.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);

        var parent = System.IO.Path.GetDirectoryName(trimmed) ?? trimmed;
        var name = System.IO.Path.GetFileName(trimmed);

        return System.IO.Path.Combine(parent, ".baseline", name);
    }

    // robocopy /MIR instead of a hand-rolled recursive copy - handles large
    // libraries efficiently (multi-threaded, only touches what changed on a
    // re-run) and its mirror semantics are exactly what a baseline needs:
    // dest ends up byte-for-byte identical to source, including deleting
    // anything in dest that source no longer has. Used both to take the
    // snapshot (source=library, dest=baseline) and to restore from it
    // (source=baseline, dest=library) - same operation, opposite direction.
    // Exit codes 0-7 are all documented robocopy successes; only 8+ is a
    // real failure.
    public static async Task MirrorDirectoryAsync(string source, string destination, System.Threading.CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(destination);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "robocopy",
            ArgumentList = { source, destination, "/MIR", "/R:1", "/W:1", "/NFL", "/NDL", "/NP", "/MT:8" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start robocopy");
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode >= 8)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"robocopy mirror from {source} to {destination} failed with exit code {process.ExitCode}: {stderr}");
        }
    }
}
