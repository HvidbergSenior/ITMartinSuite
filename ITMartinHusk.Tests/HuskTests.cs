using FluentAssertions;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Tests;

// Husk: only today and tomorrow, one thing at a time; post until read; "Nåede du det?" after a meeting; reminders once.
public class HuskTests
{
    private static readonly DateTime Thu = new(2026, 11, 12, 10, 0, 0);   // the day before the jobsamtale 13.11 09:30

    private sealed class MemoryStore : IHuskStore
    {
        public HuskData Data = new();
        public HuskData Get() => Data;
        public void Update(Action<HuskData> change) => change(Data);
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<string> Titles = [];
        public string PublicKey => "key";
        public Task<bool> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default) { Titles.Add(msg.Title); return Task.FromResult(true); }
    }

    private static (HuskUseCases Husk, MemoryStore Store, RecordingPush Push, Func<DateTime> Set) New(DateTime start)
    {
        var now = start;
        var store = new MemoryStore();
        var push = new RecordingPush();
        var husk = new HuskUseCases(store, push, () => now);
        husk.Subscribe(new PushSignup("phone", "p", "a"));
        return (husk, store, push, () => now);
    }

    [Test]
    public void Only_today_and_tomorrow_show_and_post_comes_first()
    {
        var items = new List<HuskItem>
        {
            HuskItem.Meeting("Jobsamtale", Thu.Date.AddDays(1).AddHours(9.5), "", "", "Jobnet", Thu.AddDays(-5)),
            HuskItem.Meeting("Rådighedssamtale", new DateTime(2026, 11, 26, 9, 0, 0), "", "", "A-kassen", Thu.AddDays(-5)),
            HuskItem.Post("Aarhus Kommune", Thu.AddMinutes(-3)),
        };
        var cards = HuskRules.Cards(items, Thu);
        cards.Select(c => c.Kind).Should().Equal(CardKind.Post, CardKind.Tomorrow);   // 26.11 stays hidden
        cards[1].When.Should().Be("i morgen kl. 9.30");
    }

    [Test]
    public void A_passed_meeting_asks_Did_you_go()
    {
        var m = HuskItem.Meeting("Talentmesse", Thu.AddMinutes(-20), "Musikhuset", "sundhedskort", "Jobnet", Thu.AddDays(-9));
        HuskRules.Cards([m], Thu).Single().Kind.Should().Be(CardKind.DidYouGo);
        m.Close(HuskState.Came, Thu);
        HuskRules.Cards([m], Thu).Should().BeEmpty();
    }

    [Test]
    public void Time_in_plain_words()
    {
        HuskRules.When(Thu.AddMinutes(40), Thu).Should().Be("om 40 minutter");
        HuskRules.When(Thu.AddDays(1).AddHours(-0.5), Thu).Should().Be("i morgen kl. 9.30");
        HuskRules.When(Thu.AddMinutes(-10), Thu).Should().Be("for 10 minutter siden");
        HuskRules.When(Thu.AddDays(3), Thu).Should().Be("på søndag kl. 10.00");
    }

    [Test]
    public async Task Reminders_go_evening_before_morning_and_an_hour_before_once_each()
    {
        var now = Thu;
        var store = new MemoryStore();
        var push = new RecordingPush();
        var husk = new HuskUseCases(store, push, () => now);
        husk.Subscribe(new PushSignup("phone", "p", "a"));
        husk.AddMeeting("Jobsamtale med Job Aarhus", Thu.Date.AddDays(1).AddHours(9.5), "Telefon", "", "Jobnet").Should().BeNull();

        foreach (var t in new[] { Thu.Date.AddHours(19), Thu.Date.AddHours(19.5), Thu.Date.AddDays(1).AddHours(7.5), Thu.Date.AddDays(1).AddHours(8.5), Thu.Date.AddDays(1).AddHours(9) })
        {
            now = t;
            await husk.TickAsync();
        }
        push.Titles.Should().Equal("I morgen: Jobsamtale med Job Aarhus", "I dag: Jobsamtale med Job Aarhus", "Om en time: Jobsamtale med Job Aarhus");

        now = Thu.Date.AddDays(1).AddHours(9.5).AddMinutes(16);   // a quarter past - did you go?
        await husk.TickAsync();
        await husk.TickAsync();
        push.Titles.Last().Should().StartWith("Nåede du det?");
        push.Titles.Count(t => t.StartsWith("Nåede")).Should().Be(1);
    }

    [Test]
    public async Task A_meeting_added_late_does_not_fire_the_reminders_it_has_already_passed()
    {
        var now = Thu.Date.AddHours(21);   // added the evening before at 21
        var store = new MemoryStore();
        var push = new RecordingPush();
        var husk = new HuskUseCases(store, push, () => now);
        husk.Subscribe(new PushSignup("phone", "p", "a"));
        husk.AddMeeting("Jobsamtale", Thu.Date.AddDays(1).AddHours(9.5), "", "", "");
        await husk.TickAsync();
        push.Titles.Should().BeEmpty();
    }

    [Test]
    public async Task Post_nags_at_once_and_every_half_hour_until_read_but_not_at_night()
    {
        var now = Thu;
        var store = new MemoryStore();
        var push = new RecordingPush();
        var husk = new HuskUseCases(store, push, () => now);
        husk.Subscribe(new PushSignup("phone", "p", "a"));
        husk.AddPost("Aarhus Kommune").Should().BeNull();

        await husk.TickAsync();
        now = Thu.AddMinutes(10); await husk.TickAsync();
        now = Thu.AddMinutes(31); await husk.TickAsync();
        push.Titles.Should().Equal("Ny post fra Aarhus Kommune", "Husk: Ny post fra Aarhus Kommune");

        now = Thu.Date.AddHours(23); await husk.TickAsync();   // night: quiet
        push.Titles.Should().HaveCount(2);

        husk.Set(store.Data.Items.Single().Id, HuskState.Read);
        now = Thu.Date.AddDays(1).AddHours(9); await husk.TickAsync();
        push.Titles.Should().HaveCount(2);
        husk.Today().Cards.Should().BeEmpty();
    }

    [Test]
    public void Nothing_sensitive_only_who_and_a_title()
    {
        var act = () => HuskItem.Post("   ", Thu);
        act.Should().Throw<ArgumentException>();
        var p = HuskItem.Post("Akademikernes A-kasse", Thu);
        p.Title.Should().Be("Ny post fra Akademikernes A-kasse");
        p.Place.Should().BeEmpty();
    }

    [Test]
    public void Check_round_once_a_day_from_eight()
    {
        HuskRules.CheckRoundDue("", Thu.Date.AddHours(7)).Should().BeFalse();
        HuskRules.CheckRoundDue("", Thu).Should().BeTrue();
        HuskRules.CheckRoundDue("2026-11-12", Thu).Should().BeFalse();
    }
}
