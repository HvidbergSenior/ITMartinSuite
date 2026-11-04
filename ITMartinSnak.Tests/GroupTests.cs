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
        public void Send(string room, string kind, object data) => Kinds.Add(kind);
        public (Guid Id, ChannelReader<string> Reader) Join(string room) => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<(string? Except, string Title, string Body)> Sent = [];
        public List<string> Forgotten = [];
        public string PublicKey => "test";
        public void Subscribe(string room, string endpoint, string p256dh, string auth, string name) { }
        public void Unsubscribe(string endpoint) { }
        public void SendToOthers(string room, string? exceptEndpoint, string title, string body, string tag, string url) => Sent.Add((exceptEndpoint, title, body));
        public void ForgetRoom(string room) => Forgotten.Add(room);
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

    private Group NewGroup() => new(new JsonGroupStore(_dir), _live, _push, TimeProvider.System, new RoomContext("abc", "Test", "/r/x", _ => { }));

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
        _push.Sent.Select(x => x.Title).Should().Equal("Ny opgave · Test", "Opgave taget · Test", "Opgave taget · Test");   // letting go is not pushed
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
    public void Only_the_admin_code_logs_in_and_ten_wrong_tries_lock_for_a_while()
    {
        var a = new Access("RicoAdmin", "", TimeProvider.System);
        a.Login(" RicoAdmin ", "1.2.3.4").Should().Be(LoginResult.Admin);
        a.IsAdmin(a.AdminToken).Should().BeTrue();
        a.IsApi("").Should().BeFalse();   // no API key set = the API is closed

        for (var i = 0; i < AccessRules.MaxWrongTries; i++) a.Login("forkert", "5.6.7.8").Should().Be(LoginResult.Wrong);
        a.Login("RicoAdmin", "5.6.7.8").Should().Be(LoginResult.Locked);
        a.Login("RicoAdmin", "9.9.9.9").Should().Be(LoginResult.Admin);   // other addresses are not affected

        new Access("abc", "", TimeProvider.System).Login("abc", "x").Should().Be(LoginResult.Wrong);   // a code shorter than 6 never opens
    }

    // ---- rooms (2026-11-04) ----

    private Rooms NewRooms(TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new Rooms(new JsonRoomStore(_dir, clock), _live, _push, clock, "https://snak.test/");
    }

    [Test]
    public void A_room_has_a_secret_link_and_its_messages_stay_in_that_room()
    {
        var rooms = NewRooms();
        var jytte = rooms.Create("  Jytte – høretelefoner ", "kunde-7");
        var per = rooms.Create("");
        RoomRules.ValidKey(jytte.Key).Should().BeTrue();
        jytte.Key.Should().NotBe(per.Key);
        per.Title.Should().Be("Snak med Martin");
        rooms.Link(jytte).Should().Be("https://snak.test/r/" + jytte.Key);

        rooms.GroupOf(jytte).Write("Jytte", "Hvordan lader jeg dem op?");
        rooms.GroupOf(per).MessagesAfter(0).Should().BeEmpty();

        var again = NewRooms();
        again.ByKey(jytte.Key)!.Title.Should().Be("Jytte – høretelefoner");
        again.GroupOf(again.ByKey(jytte.Key)!).MessagesAfter(0).Single().Text.Should().Be("Hvordan lader jeg dem op?");
        again.ByKey("ikke-en-rigtig-noegle").Should().BeNull();
        again.ByKey(jytte.Key[..^1] + (jytte.Key[^1] == 'a' ? 'b' : 'a')).Should().BeNull();
    }

    [Test]
    public void The_overview_puts_rooms_waiting_for_Martin_first()
    {
        var rooms = NewRooms();
        var a = rooms.Create("Svaret");
        var b = rooms.Create("Venter");
        rooms.GroupOf(a).Write("Per", "Hej");
        rooms.GroupOf(a).Write("Martin", "Hej Per", host: true);
        rooms.GroupOf(b).Write("Alexander", "Hvornår passer det?");

        var o = rooms.Overview();
        o.First().Title.Should().Be("Venter");
        o.First().Waiting.Should().BeTrue();
        o.Single(x => x.Title == "Svaret").Waiting.Should().BeFalse();
    }

    private sealed class FakeClock(DateTime start) : TimeProvider
    {
        public DateTime Now = start;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    [Test]
    public void A_finished_room_is_deleted_after_14_days_unless_someone_writes_again()
    {
        var clock = new FakeClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        var rooms = NewRooms(clock);
        var done = rooms.Create("Klaret");
        var back = rooms.Create("Kommer igen");
        rooms.MarkDone(done.Id, "Martin").Should().BeTrue();
        rooms.MarkDone(back.Id, "Martin");
        rooms.GroupOf(done).MessagesAfter(0).Single().Text.Should().Be(RoomRules.DoneText);

        clock.Now = clock.Now.AddDays(2);
        rooms.GroupOf(back).Write("Jytte", "Én ting mere");   // opens it again
        rooms.ById(back.Id)!.IsDone.Should().BeFalse();

        clock.Now = clock.Now.AddDays(13);
        rooms.DeleteFinished().Should().Be(1);
        rooms.ById(done.Id).Should().BeNull();
        rooms.ById(back.Id).Should().NotBeNull();
        Directory.Exists(Path.Combine(_dir, "rooms", done.Id)).Should().BeFalse();
        _push.Forgotten.Should().Equal(done.Id);
    }

    [Test]
    public void The_old_group_moves_into_its_own_room_and_nothing_is_lost()
    {
        Directory.CreateDirectory(_dir);
        new JsonGroupStore(_dir).SaveMessages([new Message(1, "Rico", "Hej drenge", DateTime.UtcNow)]);

        var rooms = NewRooms();
        var rico = rooms.ById(RoomRules.LegacyRoomId)!;
        rico.Title.Should().Be("Rico og venner");
        rooms.GroupOf(rico).MessagesAfter(0).Single().Text.Should().Be("Hej drenge");
        File.Exists(Path.Combine(_dir, "messages.json")).Should().BeFalse();
        NewRooms().Overview().Should().ContainSingle();   // a second start does not move it again
    }
}
