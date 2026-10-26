using FluentAssertions;
using ITMartinAdmin.Application;
using ITMartinAdmin.Contracts;
using ITMartinAdmin.Domain;

namespace ITMartinAdmin.Tests;

public class DashboardTests
{
    private sealed class Source(string app, Func<CancellationToken, Task<List<StatusLine>>> lines) : IStatusSource
    {
        public string App => app;
        public Task<List<StatusLine>> LinesAsync(CancellationToken ct) => lines(ct);
    }

    [Test]
    public async Task One_stopped_app_never_holds_the_page_and_says_it_does_not_answer()
    {
        var dash = new DashboardUseCases(
        [
            new Source("Salg", _ => Task.FromResult(new List<StatusLine> { new("ordrer at pakke", 3, true, "https://salg.bogshoppen.dk/"), new("ordrer i alt", 9) })),
            new Source("Lager", async ct => { await Task.Delay(TimeSpan.FromMinutes(1), ct); return []; }),
            new Source("Kunder", _ => throw new HttpRequestException()),
        ]);

        var started = DateTime.UtcNow;
        var answers = await dash.AnswersAsync();

        (DateTime.UtcNow - started).Should().BeLessThan(DashboardUseCases.Wait + TimeSpan.FromSeconds(2));
        answers.Select(a => (a.App, a.Answered)).Should().Equal(("Salg", true), ("Lager", false), ("Kunder", false));
        Dashboard.ToDo(answers).Should().ContainSingle().Which.Line.Count.Should().Be(3);
        Dashboard.AllQuiet(answers).Should().BeFalse();
    }

    [Test]
    public void Quiet_means_every_app_answered_and_nothing_with_a_count_needs_doing()
    {
        AppAnswer[] quiet = [new("Salg", [new("ordrer at pakke", 0, true), new("ordrer i alt", 9)], true)];
        Dashboard.AllQuiet(quiet).Should().BeTrue();
        Dashboard.AllQuiet([.. quiet, AppAnswer.Down("Lager")]).Should().BeFalse();
    }

    [Test]
    public void The_key_must_be_long_and_the_same()
    {
        AdminStatusApi.KeyOk("", "").Should().BeFalse();
        AdminStatusApi.KeyOk("kort", "kort").Should().BeFalse();
        AdminStatusApi.KeyOk("0123456789abcdef", "0123456789abcdef").Should().BeTrue();
        AdminStatusApi.KeyOk("0123456789abcdef", "forkert").Should().BeFalse();
    }

    [Test]
    public void The_links_come_in_the_group_order_and_all_go_to_https()
    {
        AdminLinks.Grouped().Select(g => g.Key).Should().Equal(AdminGroups.Order);
        AdminLinks.All.Should().OnlyContain(l => l.Url.StartsWith("https://"));
    }
}
