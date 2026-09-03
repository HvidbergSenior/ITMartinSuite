using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

// Pulled out of LibraryPolishService as its own pure, DI-free unit so the
// plateau-detection logic can be tested directly without standing up
// RunAllStepsAsync's full dependency graph (DB context, exif/duplicate/date
// services, face recognition factory, Claude client, etc.).
//
// The idea: repeated RunAllStepsAsync calls already only touch files that
// aren't IsDone yet (the doneByPath fast-skip) and quarantine unresolvable
// rotation cases into RotationUkendt, so each successive call is naturally
// cheaper and scoped to a shrinking residual - this just automates "keep
// calling it until nothing changes" instead of requiring a human to
// re-trigger it by hand each round.
public static class ConvergenceLoop
{
    public static async Task<FileStatusReport> RunAsync(
        Func<Task<FileStatusReport>> iteration,
        int maxIterations,
        CancellationToken cancellationToken = default)
    {
        if (maxIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "Must run at least one iteration.");

        var report = new FileStatusReport();
        var previousDone = -1;

        for (var i = 0; i < maxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            report = await iteration();

            // Fully converged - nothing left to do.
            if (report.TotalFiles > 0 && report.DoneFiles >= report.TotalFiles)
                break;

            // No progress since the last round - further iterations would
            // just re-pay the same (now cheap, registry-skipped) walk for
            // the same unresolved residual. Stop rather than loop forever.
            if (report.DoneFiles == previousDone)
                break;

            previousDone = report.DoneFiles;
        }

        return report;
    }
}
