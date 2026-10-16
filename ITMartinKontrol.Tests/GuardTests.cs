using FluentAssertions;
using ITMartinKontrol.Domain;

namespace ITMartinKontrol.Tests;

public class GuardTests
{
    private static readonly GuardLimits L = new(LowMb: 250, CriticalMb: 120, SwapInPerSec: 400);

    [TestCase(800, 0, Pressure.Ok)]
    [TestCase(200, 0, Pressure.Low)]
    [TestCase(490, 900, Pressure.Low)]      // the 2026-10-16 hang: RAM looked fine, the NAS was paging
    [TestCase(100, 0, Pressure.Critical)]
    public void Assess(double avail, double swapIn, Pressure expected) =>
        GuardPolicy.Assess(avail, swapIn, L).Should().Be(expected);

    [Test]
    public void Low_stops_the_biggest_can_wait_app_first()
    {
        var running = new[]
        {
            new GuardCandidate("hjem-web", Priority.MustRun, 120, false),
            new GuardCandidate("gallery-web", Priority.Important, 95, false),
            new GuardCandidate("poll-web", Priority.CanWait, 46, false),
            new GuardCandidate("upload-web", Priority.CanWait, 73, false),
        };
        GuardPolicy.PickVictim(running, Pressure.Low).Should().Be("upload-web");
    }

    [Test]
    public void Low_never_touches_important_or_must_run()
    {
        var running = new[]
        {
            new GuardCandidate("hjem-web", Priority.MustRun, 120, false),
            new GuardCandidate("gallery-web", Priority.Important, 95, false),
        };
        GuardPolicy.PickVictim(running, Pressure.Low).Should().BeNull();
    }

    [Test]
    public void Critical_also_stops_important_but_never_must_run_or_protected()
    {
        var running = new[]
        {
            new GuardCandidate("hjem-web", Priority.MustRun, 120, false),
            new GuardCandidate("cloudflared", Priority.CanWait, 300, true),
            new GuardCandidate("gallery-web", Priority.Important, 95, false),
        };
        GuardPolicy.PickVictim(running, Pressure.Critical).Should().Be("gallery-web");
    }

    [Test]
    public void Ok_stops_nothing() =>
        GuardPolicy.PickVictim([new GuardCandidate("poll-web", Priority.CanWait, 46, false)], Pressure.Ok).Should().BeNull();

    [Test]
    public void Restart_starts_must_run_then_important_then_can_wait_and_skips_protected()
    {
        var apps = new[]
        {
            new GuardCandidate("poll-web", Priority.CanWait, 0, false),
            new GuardCandidate("kontrol-web", Priority.MustRun, 0, true),
            new GuardCandidate("gallery-web", Priority.Important, 0, false),
            new GuardCandidate("hjem-web", Priority.MustRun, 0, false),
        };
        RestartPlan.StartOrder(apps).Should().Equal("hjem-web", "gallery-web", "poll-web");
    }

    [TestCase(Priority.MustRun, 100, true)]
    [TestCase(Priority.Important, 300, false)]
    [TestCase(Priority.Important, 400, true)]
    [TestCase(Priority.CanWait, 300, false)]
    public void May_start_only_with_room_to_spare(int prio, double avail, bool expected) =>
        RestartPlan.MayStart(prio, avail, L).Should().Be(expected);
}
