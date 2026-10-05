using System.Net.WebSockets;
using FluentAssertions;
using ITMartinSeMed.Server.Services;

namespace ITMartinSeMed.Tests;

public class SessionsTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static WebSocket Socket() => WebSocket.CreateFromStream(new MemoryStream(), new WebSocketCreationOptions());

    [Test]
    public void Code_is_six_digits_and_the_customer_can_join_once()
    {
        var sessions = new Sessions(new Clock());
        var s = sessions.Create(Socket());
        s.Code.Should().MatchRegex("^[0-9]{6}$");

        sessions.Join(s.Code[..3] + " " + s.Code[3..], "1.2.3.4", Socket()).result.Should().Be(JoinResult.Ok);
        sessions.Join(s.Code, "5.6.7.8", Socket()).result.Should().Be(JoinResult.Taken);
    }

    [Test]
    public void Customer_who_dropped_out_can_rejoin_with_the_same_code()
    {
        var sessions = new Sessions(new Clock());
        var s = sessions.Create(Socket());
        sessions.Join(s.Code, "ip", Socket());
        sessions.LeaveCustomer(s);
        sessions.Join(s.Code, "ip", Socket()).result.Should().Be(JoinResult.Ok);
    }

    [Test]
    public void Unjoined_code_expires_and_ended_session_is_gone()
    {
        var clock = new Clock();
        var sessions = new Sessions(clock);
        var old = sessions.Create(Socket());
        clock.Now += Sessions.JoinWindow + TimeSpan.FromMinutes(1);
        sessions.Join(old.Code, "ip", Socket()).result.Should().Be(JoinResult.Expired);

        var s = sessions.Create(Socket());
        sessions.End(s);
        sessions.Join(s.Code, "ip", Socket()).result.Should().Be(JoinResult.Unknown);
    }

    [Test]
    public void Guessing_is_stopped_per_address_and_released_after_the_window()
    {
        var clock = new Clock();
        var sessions = new Sessions(clock);
        var s = sessions.Create(Socket());
        var wrong = s.Code == "000000" ? "000001" : "000000";
        for (var i = 0; i < Sessions.MaxBadTries; i++)
            sessions.Join(wrong, "bad", Socket()).result.Should().Be(JoinResult.Unknown);

        sessions.Join(s.Code, "bad", Socket()).result.Should().Be(JoinResult.TooManyTries);
        sessions.Join(s.Code, "other", Socket()).result.Should().Be(JoinResult.Ok);

        clock.Now += Sessions.BadTryWindow + TimeSpan.FromMinutes(1);
        sessions.LeaveCustomer(s);
        sessions.Join(s.Code, "bad", Socket()).result.Should().Be(JoinResult.Ok);
    }
}
