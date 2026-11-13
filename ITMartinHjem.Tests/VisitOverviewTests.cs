using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// /admin/tal in plain words: people, new, came back, the same person, probably Martin (2026-11-13).
public class VisitOverviewTests
{
    private static readonly DateTime Since = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
    private static Hit H(string v, int day, string host = "itmartin.dk", string path = "/", string device = "mobil") =>
        new() { Visitor = v, At = new DateTime(2026, 11, day, 10, 0, 0, DateTimeKind.Utc), Host = host, Path = path, Device = device };

    private static VisitOverview.Summary Run(params Hit[] hits) => VisitOverview.Build(hits, Since, h => h);

    [Test]
    public void Counts_people_new_and_returning()
    {
        var s = Run(H("a", 10), H("a", 10), H("b", 1), H("b", 11), H("c", 7), H("c", 8), H("c", 9), H("old", 2));
        s.People.Should().Be(3);          // a, b, c (old is outside the period)
        s.New.Should().Be(1);             // a: first time, one day
        s.Returning.Should().Be(2);       // b (came back from before), c (three days)
        s.Visits.Should().Be(5);          // a 1 + b 1 + c 3 days
        s.Pages.Should().Be(6);
        s.Loyal.Should().Be(1);           // c
    }

    [Test]
    public void The_same_person_list_shows_returning_and_likely_martin_first()
    {
        var s = Run(H("a", 10), H("c", 7), H("c", 8), H("m", 9, "kigmed.itmartin.dk", "/studio"));
        s.Top.Select(p => p.Key).Should().Equal("m", "c");
        s.Top[0].LikelyOwner.Should().BeTrue();
        s.Top[0].Label.Should().Be("Person 1");
        s.Top[1].DaysInPeriod.Should().Be(2);
    }

    [Test]
    public void Owner_paths()
    {
        VisitOverview.IsOwnerPath("/studio").Should().BeTrue();
        VisitOverview.IsOwnerPath("/admin/tal").Should().BeTrue();
        VisitOverview.IsOwnerPath("/administration").Should().BeFalse();
        VisitOverview.IsOwnerPath("/").Should().BeFalse();
    }

    [Test]
    public void Hits_without_an_id_cannot_be_marked() =>
        Run(new Hit { Id = 5, At = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), Path = "/admin" }).Top[0].CanMark.Should().BeFalse();
}
