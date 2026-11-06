using FluentAssertions;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Tests;

// Bolde i luften: one ball at a time, the right one first; "Gjort" moves it on; Claude's API updates the same ball by key.
public class BoldeTests
{
    private static readonly DateTime Fri = new(2026, 11, 6, 12, 0, 0);

    private sealed class MemoryStore : IHuskStore
    {
        public HuskData Data = new();
        public HuskData Get() => Data;
        public void Update(Action<HuskData> change) => change(Data);
    }

    private static (BallUseCases Balls, MemoryStore Store) New(DateTime? at = null)
    {
        var store = new MemoryStore();
        return (new BallUseCases(store, () => at ?? Fri), store);
    }

    private static Ball B(string title, BallStatus s = BallStatus.InProgress, DateTime? deadline = null, DateTime? updated = null)
    {
        var b = Ball.Create(title, null, updated ?? Fri.AddDays(-1));
        b.Change(new BallChange(Status: s, Deadline: deadline, NextStep: "gør noget"), updated ?? Fri.AddDays(-1));
        return b;
    }

    [Test]
    public void Order_overdue_then_pinned_then_soon_then_oldest()
    {
        var old = B("Gammel", updated: Fri.AddDays(-20));
        var fresh = B("Ny", updated: Fri.AddHours(-1));
        var soon = B("Snart", deadline: Fri.AddDays(3));
        var late = B("Overskredet", BallStatus.Waiting, deadline: Fri.AddDays(-1));   // waiting, but the deadline came
        var pinned = B("Valgt");
        pinned.Pinned = true;
        var parked = B("Parkeret", BallStatus.Parked);

        BallRules.Order([fresh, parked, old, soon, pinned, late], Fri).Select(b => b.Title)
            .Should().Equal("Overskredet", "Valgt", "Snart", "Gammel", "Ny");   // parked never comes forward
    }

    [Test]
    public void Step_done_moves_the_ball_on()
    {
        var (balls, store) = New();
        balls.Add("Q-Park 975 kr", "Sager", "Send art. 15", "", null).Should().BeNull();
        var id = store.Data.Balls.Single().Id;

        balls.StepDone(id, "Betal under protest", "");
        store.Data.Balls.Single().Should().Match<Ball>(b => b.Status == BallStatus.InProgress && b.NextStep == "Betal under protest");

        balls.StepDone(id, "", "Q-Park");
        store.Data.Balls.Single().Should().Match<Ball>(b => b.Status == BallStatus.Waiting && b.WaitingOn == "Q-Park" && b.NextStep == "");

        balls.StepDone(id, "", "");
        store.Data.Balls.Single().Status.Should().Be(BallStatus.Done);
        balls.Next().Should().BeNull();
    }

    [Test]
    public void Save_matches_on_key_and_keeps_what_is_not_sent()
    {
        var (balls, store) = New();
        balls.Save([new BallInput("qpark", "Q-Park 975 kr", "Sager", "venter", WaitingOn: "Q-Park", Deadline: new DateTime(2026, 12, 1))])
            .Should().Match<BallSaveResult>(r => r.Made == 1 && r.Changed == 0 && r.Errors.Count == 0);
        var r2 = balls.Save([new BallInput("qpark", null, NextStep: "Betal under protest", Status: "i gang"), new BallInput("x", "X", Status: "måske")]);
        r2.Changed.Should().Be(1);
        r2.Errors.Should().ContainSingle().Which.Should().Contain("ukendt status");

        var b = store.Data.Balls.Single();
        b.Should().Match<Ball>(x => x.Title == "Q-Park 975 kr" && x.Area == "Sager" && x.Status == BallStatus.InProgress
                                    && x.NextStep == "Betal under protest" && x.Deadline == new DateTime(2026, 12, 1));
    }

    [Test]
    public void Pin_picks_only_one()
    {
        var (balls, store) = New();
        balls.Add("A", "", "a", "", null);
        balls.Add("B", "", "b", "", null);
        var a = store.Data.Balls[0].Id;
        var b = store.Data.Balls[1].Id;
        balls.Pin(a);
        balls.Pin(b);
        store.Data.Balls.Count(x => x.Pinned).Should().Be(1);
        balls.Next()!.Id.Should().Be(b);
        balls.Next([b])!.Id.Should().Be(a);   // "Ikke nu"
    }

    [Test]
    public void Keys_are_plain_and_unique()
    {
        BallRules.Key("Q-Park 975 kr – Æblegård").Should().Be("q-park-975-kr-aeblegaard");
        var (balls, store) = New();
        balls.Add("Ring til Lukas", "", "", "", null);
        balls.Add("Ring til Lukas", "", "", "", null);
        store.Data.Balls.Select(x => x.Key).Should().Equal("ring-til-lukas", "ring-til-lukas-2");
        balls.Add("  ", "", "", "", null).Should().Be("Skriv, hvad bolden hedder.");
    }

    [Test]
    public void Deadline_in_plain_words()
    {
        BallRules.DeadlineText(Fri.Date, Fri).Should().Be("frist i dag");
        BallRules.DeadlineText(Fri.Date.AddDays(1), Fri).Should().Be("frist i morgen");
        BallRules.DeadlineText(Fri.Date.AddDays(-3), Fri).Should().Be("fristen var for 3 dage siden");
        BallRules.DeadlineText(new DateTime(2026, 12, 1), Fri).Should().Be("frist 1. december");
        BallRules.DeadlineText(Fri.Date.AddDays(4), Fri).Should().StartWith("frist på tirsdag");
    }

    [Test]
    public void Done_balls_are_forgotten_after_60_days()
    {
        var d = new HuskData();
        var b = B("Gammel");
        b.SetStatus(BallStatus.Done, Fri.AddDays(-61));
        d.Balls.Add(b);
        d.Balls.Add(B("Åben"));
        d.Prune(Fri);
        d.Balls.Select(x => x.Title).Should().Equal("Åben");
    }

    [Test]
    public void Ball_with_deadline_reminds_the_evening_before_and_on_the_morning()
    {
        var b = B("Jobsøgning", deadline: new DateTime(2026, 11, 8));
        var sent = new List<string>();
        BallRules.Reminders([b], new DateTime(2026, 11, 7, 18, 59, 0), sent).Should().BeEmpty();
        var eve = BallRules.Reminders([b], new DateTime(2026, 11, 7, 19, 0, 0), sent);
        eve.Should().ContainSingle().Which.Message.Title.Should().Be("I morgen: Jobsøgning");
        sent.Add(eve[0].Key);
        BallRules.Reminders([b], new DateTime(2026, 11, 7, 22, 0, 0), sent).Should().BeEmpty();   // never twice
        BallRules.Reminders([b], new DateTime(2026, 11, 8, 7, 0, 0), sent).Should().BeEmpty();
        var day = BallRules.Reminders([b], new DateTime(2026, 11, 8, 12, 0, 0), sent);           // phone was off at 7.30
        day.Should().ContainSingle().Which.Message.Body.Should().Be("gør noget");
        BallRules.Reminders([b], new DateTime(2026, 11, 9, 8, 0, 0), sent.Append(day[0].Key).ToList()).Should().BeEmpty();
    }

    [Test]
    public void Done_ball_or_no_deadline_never_reminds_and_a_new_deadline_reminds_again()
    {
        var done = B("Færdig", BallStatus.Done, new DateTime(2026, 11, 8));
        var none = B("Uden frist");
        BallRules.Reminders([done, none], new DateTime(2026, 11, 8, 9, 0, 0), []).Should().BeEmpty();
        var b = B("Q-Park", deadline: new DateTime(2026, 11, 8));
        var k = BallRules.Reminders([b], new DateTime(2026, 11, 8, 9, 0, 0), []).Single().Key;
        b.Change(new BallChange(Deadline: new DateTime(2026, 11, 20)), Fri);
        BallRules.Reminders([b], new DateTime(2026, 11, 20, 9, 0, 0), [k]).Should().ContainSingle();
    }
}
