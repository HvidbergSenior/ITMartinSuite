using FluentAssertions;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Tests;

// Mine job: the a-kasse count, nothing sent twice, the API upsert, and only safe links.
public class JobTests
{
    private static readonly DateTime Fri = new(2026, 11, 6, 12, 0, 0);   // uge 45

    private sealed class MemoryStore : IHuskStore
    {
        public HuskData Data = new();
        public HuskData Get() => Data;
        public void Update(Action<HuskData> change) => change(Data);
    }

    private static (JobUseCases Jobs, MemoryStore Store) New()
    {
        var s = new MemoryStore();
        return (new JobUseCases(s, () => Fri), s);
    }

    [Test]
    public void Counts_this_week_and_the_last_four()
    {
        var (jobs, store) = New();
        jobs.Save(
        [
            new JobInput("lof", "Underviser", "LOF Aarhus", State: "søgt", AppliedAt: new DateTime(2026, 10, 23), InJoblog: true),   // uge 43
            new JobInput("tranbjerg", "Handicaphjælper", "Privat", State: "søgt", AppliedAt: new DateTime(2026, 11, 5), PartTime: true),
            new JobInput("egaa", "Højskolelærer", "Egå Ungdomshøjskole", State: "søgt", AppliedAt: new DateTime(2026, 8, 23), Advertised: false),
            new JobInput("odder", "Hjælper", "Handicapformidlingen", State: "set"),
        ]).Errors.Should().BeEmpty();
        jobs.Count().Should().Be(new JobCount(ThisWeek: 1, Last4Weeks: 2, Advertised4: 2, PartTime4: 1, NotInJoblog: 2));
    }

    [Test]
    public void Already_applied_is_found_by_employer()
    {
        var (jobs, _) = New();
        jobs.Save([new JobInput("lof", "Underviser", "LOF Aarhus", State: "søgt", AppliedAt: new DateTime(2026, 10, 23))]);
        jobs.AlreadyApplied("lof aarhus")!.Title.Should().Be("Underviser");
        jobs.AlreadyApplied("FOF Aarhus").Should().BeNull();
    }

    [Test]
    public void Save_keeps_what_is_not_sent_and_skip_clears_the_joblog_mark()
    {
        var (jobs, store) = New();
        jobs.Save([new JobInput("x", "Job", "Firma", Url: "https://jobindex.dk/a", State: "søgt", InJoblog: true)]);
        jobs.Save([new JobInput("x", null, Note: "Fik afslag")]);
        var j = store.Data.Jobs.Single();
        j.Should().Match<JobLead>(x => x.Title == "Job" && x.Url == "https://jobindex.dk/a" && x.InJoblog && x.Note == "Fik afslag");
        jobs.Skip(j.Id, "for langt væk");
        j.Should().Match<JobLead>(x => x.State == JobState.Skipped && !x.InJoblog && x.AppliedAt == null && x.Note == "for langt væk");
    }

    [Test]
    public void Only_safe_links()
    {
        var (jobs, store) = New();
        jobs.Add("A", "B", "", "javascript:alert(1)", true, false);
        store.Data.Jobs.Single().Url.Should().BeEmpty();
        var b = Ball.Create("Jobsøgning", null, Fri);
        b.Change(new BallChange(Link: "//evil.example"), Fri);
        b.Link.Should().BeEmpty();
        b.Change(new BallChange(Link: "/job"), Fri);
        b.Link.Should().Be("/job");
    }
}
