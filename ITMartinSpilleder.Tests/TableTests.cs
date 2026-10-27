using System.Threading.Channels;
using FluentAssertions;
using ITMartinSpilleder.Application;
using ITMartinSpilleder.Domain;
using ITMartinSpilleder.Infrastructure;

namespace ITMartinSpilleder.Tests;

// The tables (Application) with the real JSON files and pictures (Infrastructure) and recording fakes for live feed and push.
public class TableTests
{
    private string _dir = "";
    private RecordingLive _live = null!;
    private RecordingPush _push = null!;

    private sealed class RecordingLive : ILiveFeed
    {
        public List<(string Kind, object Data, Func<string, bool>? To)> Sent = [];
        public (Guid Id, ChannelReader<string> Reader) Join(string gameId, string viewer) => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
        public void Send(string gameId, string kind, object data, Func<string, bool>? to = null) => Sent.Add((kind, data, to));
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<(Func<string, bool> To, string Title, string Body)> Sent = [];
        public string PublicKey => "test";
        public void Subscribe(string gameId, string viewer, string endpoint, string p256dh, string auth) { }
        public void Unsubscribe(string endpoint) { }
        public void Send(string gameId, Func<string, bool> to, string title, string body) => Sent.Add((to, title, body));
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "spil-test-" + Guid.NewGuid().ToString("N"));
        _live = new RecordingLive();
        _push = new RecordingPush();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private Tables NewTables() => new(new JsonGameStore(Path.Combine(_dir, "spil")), _live, _push,
        new FilePictureStore(Path.Combine(_dir, "billeder")), TimeProvider.System, "opret123");

    private static (Tables T, string Game, string A, string B) Table(Tables t)
    {
        var (lead, _) = t.Create("opret123", "Dragens hule", "Julius", "1");
        var code = t.View(lead!.GameId, Viewer.Leader)!.JoinCode;
        var a = t.Join(code, "Anna", "2").Joined!;
        var b = t.Join(code.ToLowerInvariant(), "Bo", "3").Joined!;
        return (t, lead.GameId, a.Viewer, b.Viewer);
    }

    [Test]
    public void Wrong_create_code_is_refused()
    {
        NewTables().Create("forkert", "x", "Julius", "1").Error.Should().Be(JoinError.WrongCode);
    }

    [Test]
    public void Secret_message_is_seen_only_by_that_player_and_the_leader()
    {
        var (t, g, a, b) = Table(NewTables());
        t.Write(g, Viewer.Leader, [a], "Du ser noget glimte til højre").Should().NotBeNull();

        t.View(g, a)!.Notes.Should().ContainSingle(n => n.Text.Contains("glimte"));
        t.View(g, b)!.Notes.Should().BeEmpty();
        t.View(g, Viewer.Leader)!.Notes.Should().HaveCount(1);

        var live = _live.Sent.Single(s => s.Kind == "besked");
        live.To!(a).Should().BeTrue();
        live.To!(b).Should().BeFalse();
        _push.Sent.Last().To(b).Should().BeFalse();
    }

    [Test]
    public void A_player_cannot_whisper_to_another_player()
    {
        var (t, g, a, b) = Table(NewTables());
        t.Write(g, a, [b], "hemmeligt").Should().BeNull();
        t.Write(g, a, [Viewer.Leader], "jeg stjæler ringen")!.To.Should().Equal(Viewer.Leader);
        t.View(g, b)!.Notes.Should().BeEmpty();
    }

    [Test]
    public void Everyone_sees_every_card_and_hp_changes_go_live()
    {
        var (t, g, a, b) = Table(NewTables());
        t.EditCard(g, a, a, "Anna", "Pip", "Halvling", "Tyv", 8, 8, "reb og dirk", "kortsværd", [new Spell("Mage Hand", "en usynlig hånd")]);
        t.ChangeHp(g, b, a, -3).Should().BeNull("a player may not change another player's card");
        t.ChangeHp(g, Viewer.Leader, a, -3)!.Hp.Should().Be(5);

        var seenByB = t.View(g, b)!.Players.Single(p => p.Id == a);
        seenByB.Race.Should().Be("Halvling");
        seenByB.Hp.Should().Be(5);
        seenByB.Spells.Should().ContainSingle();
        _live.Sent.Should().Contain(s => s.Kind == "hp");
    }

    [Test]
    public void A_saved_card_is_locked_until_the_leader_allows_a_change()
    {
        var (t, g, a, _) = Table(NewTables());
        t.EditCard(g, a, a, "Anna", "Pip", "Halvling", "Tyv", 8, 8, "", "kortsværd", [])!.Locked.Should().BeTrue();
        t.EditCard(g, a, a, "Anna", "Pip", "Halvling", "Tyv", 8, 8, "", "langsværd", []).Should().BeNull("locked after saving");
        var jpeg = new byte[500]; jpeg[0] = 0xFF; jpeg[1] = 0xD8; jpeg[2] = 0xFF;
        t.SetPicture(g, a, a, jpeg).Should().BeNull("the picture is on the sheet too");
        t.ChangeHp(g, a, a, -2)!.Hp.Should().Be(6, "HP is play, not the sheet");

        t.SetLocked(g, a, a, false).Should().BeNull("only the leader unlocks");
        t.SetLocked(g, Viewer.Leader, a, false)!.Locked.Should().BeFalse();
        t.EditCard(g, a, a, "Anna", "Pip", "Halvling", "Tyv", 6, 8, "", "langsværd", [])!.Gear.Should().Be("langsværd");
        t.View(g, a)!.Players.Single(p => p.Id == a).Locked.Should().BeTrue("locked again after the change");
        t.EditCard(g, Viewer.Leader, a, "Anna", "Pip", "Halvling", "Tyv", 6, 8, "forbandet", "langsværd", []).Should().NotBeNull("the leader may always edit");
    }

    [Test]
    public void Hp_never_goes_above_max()
    {
        var (t, g, a, _) = Table(NewTables());
        t.ChangeHp(g, a, a, 50)!.Hp.Should().Be(10);
    }

    [Test]
    public void Games_and_pictures_survive_a_restart()
    {
        var (t, g, a, _) = Table(NewTables());
        var jpeg = new byte[500]; jpeg[0] = 0xFF; jpeg[1] = 0xD8; jpeg[2] = 0xFF;
        t.SetPicture(g, a, a, jpeg)!.Picture.Should().Be(1);
        t.SetPicture(g, a, a, [1, 2, 3]).Should().BeNull("not a JPEG");
        t.EditCard(g, a, a, "Anna", "Pip", "Halvling", "Tyv", 8, 8, "", "", []);

        var again = NewTables();
        again.View(g, a)!.Players.Single(p => p.Id == a).Character.Should().Be("Pip");
        again.Picture(g, a).Should().Equal(jpeg);
    }

    [Test]
    public void Leader_code_lets_the_leader_in_on_another_phone()
    {
        var (t, g, _, _) = Table(NewTables());
        var leaderCode = t.View(g, Viewer.Leader)!.LeaderCode;
        t.Join(leaderCode, "", "9").Joined!.Viewer.Should().Be(Viewer.Leader);
        t.View(g, Viewer.Leader)!.Players.Should().HaveCount(2, "the leader is not added as a player");
    }

    [Test]
    public void Hidden_roll_is_seen_only_by_the_leader()
    {
        var (t, g, a, b) = Table(NewTables());
        t.Roll(g, a, "1d20+2", true, "snig").Should().NotBeNull();
        t.Roll(g, Viewer.Leader, "d6", true, null)!.To.Should().Equal(Viewer.Leader);
        t.View(g, b)!.Notes.Should().BeEmpty();
        t.View(g, Viewer.Leader)!.Notes.Should().HaveCount(2);
        t.Roll(g, a, "fisk", false, null).Should().BeNull();
    }

    [Test]
    public void Dice_text_is_read_right()
    {
        var r = GameRules.Roll("2d6+1", n => n - 1)!.Value;
        r.Rolls.Should().Equal(6, 6);
        r.Total.Should().Be(13);
        GameRules.Roll("d20", _ => 0)!.Value.Total.Should().Be(1);
        GameRules.Roll("50d6", _ => 0).Should().BeNull();
    }
}
