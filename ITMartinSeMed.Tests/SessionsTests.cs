using FluentAssertions;
using ITMartinSeMed.Application;
using ITMartinSeMed.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinSeMed.Tests;

public class SessionsTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // A browser line in the tests: open until "dropped", and it records what it is sent.
    private sealed class Line : IPeer
    {
        public bool IsOpen { get; set; } = true;
        public List<string> Got { get; } = [];
        public Task SendAsync(string json) { Got.Add(json); return Task.CompletedTask; }
        public void Abort() => IsOpen = false;
    }

    private static Line Socket() => new();

    [Test]
    public void Code_is_six_digits_and_the_customer_can_join_once()
    {
        var sessions = new SessionRegistry(new Clock());
        var s = sessions.Create(Socket());
        s.Code.Should().MatchRegex("^[0-9]{6}$");

        sessions.Join(s.Code[..3] + " " + s.Code[3..], "1.2.3.4", Socket()).Result.Should().Be(JoinResult.Ok);
        sessions.Join(s.Code, "5.6.7.8", Socket()).Result.Should().Be(JoinResult.Taken);
    }

    [Test]
    public void Customer_who_dropped_out_can_rejoin_with_the_same_code()
    {
        var sessions = new SessionRegistry(new Clock());
        var s = sessions.Create(Socket());
        sessions.Join(s.Code, "ip", Socket());
        sessions.LeaveCustomer(s);
        sessions.Join(s.Code, "ip", Socket()).Result.Should().Be(JoinResult.Ok);
    }

    [Test]
    public void Unjoined_code_expires_and_ended_session_is_gone()
    {
        var clock = new Clock();
        var sessions = new SessionRegistry(clock);
        var old = sessions.Create(Socket());
        clock.Now += SessionRules.JoinWindow + TimeSpan.FromMinutes(1);
        sessions.Join(old.Code, "ip", Socket()).Result.Should().Be(JoinResult.Expired);

        var s = sessions.Create(Socket());
        sessions.End(s);
        sessions.Join(s.Code, "ip", Socket()).Result.Should().Be(JoinResult.Unknown);
    }

    [Test]
    public void Guessing_is_stopped_per_address_and_released_after_the_window()
    {
        var clock = new Clock();
        var sessions = new SessionRegistry(clock);
        var s = sessions.Create(Socket());
        var wrong = s.Code == "000000" ? "000001" : "000000";
        for (var i = 0; i < SessionRules.MaxBadTries; i++)
            sessions.Join(wrong, "bad", Socket()).Result.Should().Be(JoinResult.Unknown);

        sessions.Join(s.Code, "bad", Socket()).Result.Should().Be(JoinResult.TooManyTries);
        sessions.Join(s.Code, "other", Socket()).Result.Should().Be(JoinResult.Ok);

        clock.Now += SessionRules.BadTryWindow + TimeSpan.FromMinutes(1);
        sessions.LeaveCustomer(s);
        sessions.Join(s.Code, "bad", Socket()).Result.Should().Be(JoinResult.Ok);
    }

    [Test]
    public void Customer_reconnect_replaces_her_own_dead_line()
    {
        var sessions = new SessionRegistry(new Clock());
        var s = sessions.Create(Socket());
        var first = Socket();
        sessions.Join(s.Code, "ip", first);
        first.Abort();   // her line dropped before the server noticed
        sessions.Join(s.Code, "ip", Socket()).Result.Should().Be(JoinResult.Ok);
    }

    [Test]
    public void Session_waits_for_Martin_to_reconnect_within_the_grace_time()
    {
        var clock = new Clock();
        var sessions = new SessionRegistry(clock);
        var helper = Socket();
        var s = sessions.Create(helper);
        sessions.Join(s.Code, "ip", Socket());

        sessions.LeaveHelper(s, helper);
        clock.Now += TimeSpan.FromMinutes(5);
        sessions.HelperTimedOut(s).Should().BeFalse();
        var back = Socket();
        sessions.Resume(s.Code, back).Should().BeSameAs(s);
        s.Helper.Should().BeSameAs(back);

        sessions.LeaveHelper(s, helper);   // the old line closing late must not unseat the new one
        s.Helper.Should().BeSameAs(back);
    }

    [Test]
    public void Session_ends_when_Martin_does_not_come_back()
    {
        var clock = new Clock();
        var sessions = new SessionRegistry(clock);
        var helper = Socket();
        var s = sessions.Create(helper);
        sessions.LeaveHelper(s, helper);
        clock.Now += SessionRules.HelperGrace + TimeSpan.FromSeconds(1);
        sessions.HelperTimedOut(s).Should().BeTrue();
        sessions.Resume(s.Code, Socket()).Should().BeNull();
    }

    [Test]
    public async Task Rooms_give_Martin_a_code_let_the_customer_in_and_pass_the_handshake_on()
    {
        var clock = new Clock();
        var rooms = new Rooms(new SessionRegistry(clock), clock, NullLogger<Rooms>.Instance);
        var martin = Socket();
        var s = await rooms.HelperArrivedAsync(martin, null);
        martin.Got.Should().ContainSingle().Which.Should().Contain("\"kode\"").And.Contain(s.Code);

        var her = Socket();
        (await rooms.CustomerArrivedAsync(her, "000000" == s.Code ? "000001" : "000000", "ip")).Should().BeNull();
        her.Got.Last().Should().Contain("ukendt");
        her = Socket();
        (await rooms.CustomerArrivedAsync(her, s.Code, "ip")).Should().BeSameAs(s);
        her.Got.Should().ContainSingle().Which.Should().Contain("\"ok\"");
        martin.Got.Last().Should().Contain("kunde-ind");

        await rooms.ForwardAsync(s, her, "{\"t\":\"offer\"}");
        await rooms.ForwardAsync(s, martin, "{\"t\":\"answer\"}");
        martin.Got.Last().Should().Be("{\"t\":\"offer\"}");
        her.Got.Last().Should().Be("{\"t\":\"answer\"}");
    }

    [Test]
    public async Task Rooms_tell_the_customer_when_Martin_drops_out_comes_back_and_stops()
    {
        var clock = new Clock();
        var rooms = new Rooms(new SessionRegistry(clock), clock, NullLogger<Rooms>.Instance);
        var martin = Socket();
        var s = await rooms.HelperArrivedAsync(martin, null);
        var her = Socket();
        await rooms.CustomerArrivedAsync(her, s.Code, "ip");

        martin.Abort();
        await rooms.HelperLeftAsync(s, martin, endedOnPurpose: false);
        her.Got.Last().Should().Contain("martin-ud");

        var back = Socket();
        (await rooms.HelperArrivedAsync(back, s.Code)).Should().BeSameAs(s, "same code, same session");
        back.Got.Should().Contain(m => m.Contains("kunde-ind"));
        her.Got.Last().Should().Contain("martin-ind");

        await rooms.HelperLeftAsync(s, back, endedOnPurpose: true);
        her.Got.Last().Should().Contain("slut");
        (await rooms.CustomerArrivedAsync(Socket(), s.Code, "ip2")).Should().BeNull("the session is over");
    }

    [Test]
    public async Task A_customer_reload_is_not_reported_as_leaving()
    {
        var clock = new Clock();
        var rooms = new Rooms(new SessionRegistry(clock), clock, NullLogger<Rooms>.Instance);
        var martin = Socket();
        var s = await rooms.HelperArrivedAsync(martin, null);
        var first = Socket();
        await rooms.CustomerArrivedAsync(first, s.Code, "ip");
        first.Abort();
        var second = Socket();
        await rooms.CustomerArrivedAsync(second, s.Code, "ip");
        var before = martin.Got.Count;
        await rooms.CustomerLeftAsync(s, first);   // the old line closing late
        martin.Got.Should().HaveCount(before, "her new line is still in");
        await rooms.CustomerLeftAsync(s, second);
        martin.Got.Last().Should().Contain("kunde-ud");
    }
}
