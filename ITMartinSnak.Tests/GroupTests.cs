using System.Threading.Channels;
using FluentAssertions;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

namespace ITMartinSnak.Tests;

// The group (Application) with the real JSON files (Infrastructure) and recording fakes for live feed and push.
public class GroupTests
{
    private string _dir = "";
    private RecordingLive _live = null!;
    private RecordingPush _push = null!;

    private sealed class RecordingLive : ILiveFeed
    {
        public List<string> Kinds = [];
        public void Send(string kind, object data) => Kinds.Add(kind);
        public (Guid Id, ChannelReader<string> Reader) Join() => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<(string? Except, string Title, string Body)> Sent = [];
        public string PublicKey => "test";
        public void Subscribe(string endpoint, string p256dh, string auth, string name) { }
        public void Unsubscribe(string endpoint) { }
        public void SendToOthers(string? exceptEndpoint, string title, string body, string tag) => Sent.Add((exceptEndpoint, title, body));
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "snak-test-" + Guid.NewGuid().ToString("N"));
        _live = new RecordingLive();
        _push = new RecordingPush();
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

    private Group NewGroup() => new(new JsonGroupStore(_dir), _live, _push, TimeProvider.System);

    [Test]
    public void Messages_survive_a_restart_and_come_after_the_last_seen_id()
    {
        var g = NewGroup();
        g.Write("Martin", "Hej alle").Should().NotBeNull();
        g.Write("Sonja", "  Hej!  ")!.Text.Should().Be("Hej!");

        var again = NewGroup();
        again.MessagesAfter(0).Select(m => m.Name).Should().Equal("Martin", "Sonja");
        again.MessagesAfter(1).Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Test]
    public void A_message_needs_a_name_and_text_and_goes_live_and_to_the_other_phones()
    {
        var g = NewGroup();
        g.Write("", "hej").Should().BeNull();
        g.Write("Martin", "   ").Should().BeNull();
        g.Write(new string('x', 50), "hej", "https://push/martin")!.Name.Should().HaveLength(GroupRules.MaxName);

        _live.Kinds.Should().Equal("besked");
        _push.Sent.Should().ContainSingle().Which.Except.Should().Be("https://push/martin");   // not back to the sender
    }

    [Test]
    public void Taking_a_task_twice_lets_go_of_it_again()
    {
        var g = NewGroup();
        var t = g.AddTask("Køb kaffe", "", "lørdag")!;

        g.ToggleTaker(t.Id, "Jan")!.Takers.Should().Equal("Jan");
        g.ToggleTaker(t.Id, "Sonja")!.Takers.Should().Equal("Jan", "Sonja");
        g.ToggleTaker(t.Id, "jan")!.Takers.Should().Equal("Sonja");
        NewGroup().Tasks().Single().Takers.Should().Equal("Sonja");
        _push.Sent.Select(x => x.Title).Should().Equal("Ny opgave", "Opgave taget", "Opgave taget");   // letting go is not pushed
    }

    [Test]
    public void Tasks_can_be_edited_finished_and_deleted()
    {
        var g = NewGroup();
        var t = g.AddTask("Rydde op", "", "")!;
        g.AddTask(" ", "", "").Should().BeNull();

        g.EditTask(t.Id, "Rydde op i haven", "Tag handsker med", "søndag")!.When.Should().Be("søndag");
        g.EditTask(t.Id, "  ", "", "").Should().BeNull();
        g.SetDone(t.Id, true)!.Done.Should().BeTrue();
        g.DeleteTask(t.Id).Should().BeTrue();
        g.Tasks().Should().BeEmpty();
        g.DeleteTask(t.Id).Should().BeFalse();
    }

    [Test]
    public void Codes_make_members_and_admins_and_ten_wrong_tries_lock_for_a_while()
    {
        var a = new Access("Rico", "RicoAdmin", TimeProvider.System);
        a.Login(" Rico ", "1.2.3.4").Should().Be(LoginResult.Member);
        a.Login("RicoAdmin", "1.2.3.4").Should().Be(LoginResult.Admin);
        a.IsMember(a.MemberToken, null).Should().BeTrue();
        a.IsAdmin(a.MemberToken).Should().BeFalse();
        a.IsMember(null, a.AdminToken).Should().BeTrue();

        for (var i = 0; i < AccessRules.MaxWrongTries; i++) a.Login("forkert", "5.6.7.8").Should().Be(LoginResult.Wrong);
        a.Login("Rico", "5.6.7.8").Should().Be(LoginResult.Locked);
        a.Login("Rico", "9.9.9.9").Should().Be(LoginResult.Member);   // other addresses are not affected

        new Access("abc", "", TimeProvider.System).Login("abc", "x").Should().Be(LoginResult.Wrong);   // a code shorter than 4 never opens
    }
}
