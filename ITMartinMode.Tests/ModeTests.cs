using FluentAssertions;
using ITMartinMode.Application;
using ITMartinMode.Domain;

namespace ITMartinMode.Tests;

public class ModeTests
{
    private sealed class MemStore : IBoardStore
    {
        public Board Load(string slug) => new();
        public void Save(string slug, Board board) { }
    }

    private static DateTimeOffset _now = new(2026, 11, 27, 10, 30, 0, TimeSpan.Zero);
    private static MeetingService Service() =>
        new([new RoomSetup("gruppe1", "Følgegruppen", "hemmelig")], new MemStore(), () => _now);

    [Test]
    public void Wrong_key_does_not_open_the_door()
    {
        var m = Service();
        m.Door("gruppe1", "forkert").Should().BeNull();
        m.Door("gruppe1", null).Should().BeNull();
        m.Door("GRUPPE1", "hemmelig").Should().NotBeNull();
    }

    [Test]
    public void Only_the_person_themself_may_publish_their_path()
    {
        var m = Service();
        var r = m.Door("gruppe1", "hemmelig")!;
        var a = m.Join(r, "Martin");
        var b = m.Join(r, "Pernille");
        m.CanPublish(r, a.Id, a.Token, a.MediaPath("gruppe1")).Should().BeTrue();
        m.CanPublish(r, a.Id, a.Token, b.MediaPath("gruppe1")).Should().BeFalse();
        m.CanPublish(r, a.Id, "gaet", a.MediaPath("gruppe1")).Should().BeFalse();
    }

    [Test]
    public void Notes_need_a_person_in_the_room_and_some_text()
    {
        var m = Service();
        var r = m.Door("gruppe1", "hemmelig")!;
        var a = m.Join(r, "Martin");
        m.AddNote(r, a.Id, a.Token, StarPoint.Hvem, "Kunder på loppemarkedet");
        m.View(r).Notes.Should().ContainSingle(n => n.Point == "Hvem" && n.Author == "Martin");
        FluentActions.Invoking(() => m.AddNote(r, a.Id, a.Token, StarPoint.Hvem, "  ")).Should().Throw<DomainException>();
        FluentActions.Invoking(() => m.AddNote(r, "x", "y", StarPoint.Hvem, "hej")).Should().Throw<DomainException>();
    }

    [Test]
    public void Quiet_people_leave_after_a_while()
    {
        var m = Service();
        var r = m.Door("gruppe1", "hemmelig")!;
        m.Join(r, "Evald");
        _now = _now.AddMinutes(2);
        m.SweepAll();
        m.View(r).People.Should().BeEmpty();
    }

    [Test]
    public void Game_owner_tells_then_the_others_answer_one_point_each_and_the_owner_moves_on()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var g = new Game([new("a", "A"), new("b", "B"), new("c", "C"), new("d", "D")], t0);
        g.Speaker.Id.Should().Be("a");          // A tells the idea
        g.Left(t0).Should().BeNull();           // no timer for the idea
        var speakers = new List<string>();
        for (var i = 0; i < 6; i++) { g.Next(t0); speakers.Add(g.Speaker.Id); g.Left(t0).Should().Be(TimeSpan.FromMinutes(1)); }
        speakers.Should().NotContain("a").And.Equal("b", "c", "d", "b", "c", "d");
        g.Point.Should().Be(StarPoint.Hvordan);
        g.Next(t0);
        g.Owner.Id.Should().Be("b");            // round 2: B's idea
        g.Speaker.Id.Should().Be("b");
        g.Next(t0);
        g.Speaker.Id.Should().NotBe("b");
    }

    [Test]
    public void Game_ends_when_everybody_has_had_an_idea()
    {
        var g = new Game([new("a", "A"), new("b", "B")], DateTimeOffset.UnixEpoch);
        for (var i = 0; i < 2 * Game.Steps; i++) g.Next(DateTimeOffset.UnixEpoch);
        g.Finished.Should().BeTrue();
    }
}
