using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Pipelines.Package3;

namespace ITMartinFileSorter.Tests.Package1Tests;

// Covers RunUntilConvergedAsync's core plateau-detection logic, added
// 2026-08-24 to automate "keep calling RunAllStepsAsync round after round
// until the residual stops shrinking" instead of requiring a human to
// re-trigger it by hand. Tested against a plain Func<Task<FileStatusReport>>
// rather than the real RunAllStepsAsync, which needs a full DI graph
// (DB context, exif/duplicate/date services, face recognition factory,
// Claude client) to exercise directly.
[TestFixture]
public class ConvergenceLoopTests
{
    private static FileStatusReport Report(int total, int done) =>
        new() { TotalFiles = total, DoneFiles = done };

    [Test]
    public async Task Stops_as_soon_as_every_file_is_done()
    {
        var calls = 0;
        var results = new[] { Report(100, 40), Report(100, 100), Report(100, 100) };

        var final = await ConvergenceLoop.RunAsync(
            () => Task.FromResult(results[calls++]),
            maxIterations: 10);

        calls.Should().Be(2, "it should stop the round it reaches TotalFiles, not keep going");
        final.DoneFiles.Should().Be(100);
    }

    [Test]
    public async Task Stops_when_a_round_makes_zero_additional_progress()
    {
        var calls = 0;
        var results = new[] { Report(100, 40), Report(100, 70), Report(100, 70), Report(100, 70) };

        var final = await ConvergenceLoop.RunAsync(
            () => Task.FromResult(results[calls++]),
            maxIterations: 10);

        calls.Should().Be(3, "it should stop the first round DoneFiles repeats, not keep re-running a plateaued residual");
        final.DoneFiles.Should().Be(70);
    }

    [Test]
    public async Task Keeps_going_while_each_round_still_makes_progress()
    {
        var calls = 0;
        var results = new[] { Report(100, 10), Report(100, 25), Report(100, 45), Report(100, 60) };

        var final = await ConvergenceLoop.RunAsync(
            () => Task.FromResult(results[calls++]),
            maxIterations: 4);

        calls.Should().Be(4);
        final.DoneFiles.Should().Be(60);
    }

    [Test]
    public async Task Never_exceeds_maxIterations_even_if_still_improving_every_round()
    {
        var calls = 0;

        var final = await ConvergenceLoop.RunAsync(
            () =>
            {
                calls++;
                // Always makes exactly one more file "done" than last time -
                // would loop forever without the hard ceiling.
                return Task.FromResult(Report(1000, calls * 10));
            },
            maxIterations: 5);

        calls.Should().Be(5);
        final.DoneFiles.Should().Be(50);
    }

    [Test]
    public void Rejects_a_maxIterations_below_one()
    {
        var act = () => ConvergenceLoop.RunAsync(() => Task.FromResult(Report(10, 10)), maxIterations: 0);

        act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task A_single_iteration_that_already_fully_converges_only_runs_once()
    {
        var calls = 0;

        var final = await ConvergenceLoop.RunAsync(
            () => { calls++; return Task.FromResult(Report(50, 50)); },
            maxIterations: 10);

        calls.Should().Be(1);
        final.DoneFiles.Should().Be(50);
    }
}
