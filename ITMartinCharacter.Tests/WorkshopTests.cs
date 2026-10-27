using System.Threading.Channels;
using FluentAssertions;
using ITMartinCharacter.Application;
using ITMartinCharacter.Domain;
using ITMartinCharacter.Infrastructure;

namespace ITMartinCharacter.Tests;

// The workshop (Application) with the real JSON files and pictures (Infrastructure) and a recording fake for the live feed.
public class WorkshopTests
{
    private string _dir = "";
    private RecordingLive _live = null!;

    private sealed class RecordingLive : ILiveFeed
    {
        public List<(string Kind, object Data, Func<string, bool>? To)> Sent = [];
        public (Guid Id, ChannelReader<string> Reader) Join(string figureId, string viewer) => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
        public void Send(string figureId, string kind, object data, Func<string, bool>? to = null) => Sent.Add((kind, data, to));
    }

    private sealed class FakeCustomers : ICustomerCheck
    {
        public Task<(bool Ok, string Name, string Error)> CheckAsync(string customerNo, CancellationToken ct) =>
            Task.FromResult(customerNo == "11112222" ? (true, "Julius", "") : (false, "", "We don't know that customer number."));
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "character-test-" + Guid.NewGuid().ToString("N"));
        _live = new RecordingLive();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private Workshop NewWorkshop() => new(new JsonFigureStore(Path.Combine(_dir, "figurer")), new FilePictureStore(Path.Combine(_dir, "billeder")),
        _live, new FakeCustomers(), TimeProvider.System);

    private static async Task<(Workshop W, string Id, string PlayerCode, string MasterCode)> NewFigure(Workshop w, string kind = "dnd")
    {
        var (o, e, _) = await w.CreateAsync("Anna", kind, null, "1.1.1.1", default);
        e.Should().Be(OpenError.None);
        var v = w.View(o!.FigureId, Viewers.Player)!;
        return (w, o.FigureId, v.PlayerCode, v.MasterCode);
    }

    [Test]
    public async Task Player_code_and_master_code_open_the_same_figure_as_player_and_master()
    {
        var (w, id, pc, mc) = await NewFigure(NewWorkshop());
        w.Open(pc, "ip").Opened.Should().BeEquivalentTo(new Opened(id, Viewers.Player, pc));
        w.Open(mc.ToLowerInvariant(), "ip").Opened!.Viewer.Should().Be(Viewers.Master);
        w.Open("WRONGCOD", "ip").Error.Should().Be(OpenError.WrongCode);
    }

    [Test]
    public async Task Ten_wrong_codes_lock_the_address()
    {
        var w = NewWorkshop();
        for (var i = 0; i < Workshop.MaxWrongTries; i++) w.Open("AAAAAAAA", "9.9.9.9");
        var (_, _, pc, _) = await NewFigure(w);
        w.Open(pc, "9.9.9.9").Error.Should().Be(OpenError.Locked);
        w.Open(pc, "8.8.8.8").Error.Should().Be(OpenError.None);
    }

    [Test]
    public async Task Every_change_is_saved_at_once_and_survives_a_restart()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetText(id, Viewers.Player, "name", "Thorin").Should().NotBeNull();
        w.SetText(id, Viewers.Player, "people", "Dwarf");
        NewWorkshop().View(id, Viewers.Player)!.Name.Should().Be("Thorin");
    }

    [Test]
    public async Task Only_the_game_master_writes_and_sees_the_master_notes()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetText(id, Viewers.Player, "masterNotes", "peek").Should().BeNull();
        w.SetText(id, Viewers.Master, "masterNotes", "Secret heir").Should().NotBeNull();
        w.View(id, Viewers.Player)!.MasterNotes.Should().BeNull();
        w.View(id, Viewers.Master)!.MasterNotes.Should().Be("Secret heir");
        // The live copy for the player has no notes either.
        _live.Sent.Where(s => s.Kind == "figur" && s.To!(Viewers.Player)).Select(s => ((FigureView)s.Data).MasterNotes).Should().AllSatisfy(n => n.Should().BeNull());
    }

    [Test]
    public async Task Standard_array_uses_each_number_once()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetAbilities(id, Viewers.Player, "array", [15, 14, 0, 0, 0, 0]).Should().NotBeNull();
        w.SetAbilities(id, Viewers.Player, "array", [15, 15, 13, 12, 10, 8]).Should().BeNull();
        w.SetAbilities(id, Viewers.Player, "array", [16, 14, 13, 12, 10, 8]).Should().BeNull();
    }

    [Test]
    public async Task Rolled_scores_must_come_from_the_servers_rolls()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        var v = w.RollAbilities(id, Viewers.Player)!;
        v.Rolls.Should().HaveCount(6).And.AllSatisfy(r => r.Should().BeInRange(3, 18));
        w.SetAbilities(id, Viewers.Player, "roll", v.Rolls.ToArray()).Should().NotBeNull();
        var cheat = v.Rolls.Select(_ => 18).ToArray();
        if (!v.Rolls.All(r => r == 18)) w.SetAbilities(id, Viewers.Player, "roll", cheat).Should().BeNull();
    }

    [Test]
    public async Task People_bonus_and_class_give_the_start_hit_points()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetText(id, Viewers.Player, "people", "Dwarf");
        w.SetText(id, Viewers.Player, "class", "fighter");
        var v = w.SetAbilities(id, Viewers.Player, "array", [15, 13, 14, 12, 10, 8])!;
        v.Final[2].Should().Be(16);              // CON 14 + 2 for a dwarf
        v.SuggestedHp.Should().Be(10 + 3);        // d10 + CON modifier (+3)
    }

    [Test]
    public async Task Ready_locks_the_figure_for_the_player_until_the_game_master_opens_it()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.MakeReady(id, Viewers.Player).Should().BeNull("a figure needs a name first");
        w.SetText(id, Viewers.Player, "name", "Thorin");
        var v = w.MakeReady(id, Viewers.Player)!;
        v.FigureCode.Should().HaveLength(6);
        v.Locked.Should().BeTrue();
        w.SetText(id, Viewers.Player, "gear", "axe").Should().BeNull();
        w.SetText(id, Viewers.Master, "gear", "axe").Should().NotBeNull();
        w.SetLocked(id, Viewers.Player, false).Should().BeNull();
        w.SetLocked(id, Viewers.Master, false);
        w.SetText(id, Viewers.Player, "gear", "axe, rope").Should().NotBeNull();
    }

    [Test]
    public async Task Game_Master_imports_the_ready_figure_and_sends_back_what_happened()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetText(id, Viewers.Player, "name", "Thorin");
        w.SetText(id, Viewers.Player, "story", "Lost his hammer.");
        var code = w.View(id, Viewers.Player)!.FigureCode;
        code.Should().BeEmpty();
        code = w.MakeReady(id, Viewers.Player)!.FigureCode;

        var a = w.ForAdventure(code.ToLowerInvariant())!;
        a.Name.Should().Be("Thorin");
        a.Sheet.Should().Contain("Lost his hammer.");

        w.BackFromAdventure(code, 4, 12, "axe\nmap", "Found the hammer in the mine.").Should().BeTrue();
        var v = w.View(id, Viewers.Player)!;
        (v.Hp, v.MaxHp, v.Gear, v.AdventureNotes).Should().Be((4, 12, "axe\nmap", "Found the hammer in the mine."));
        v.Story.Should().Be("Lost his hammer.");
        w.ForAdventure("ZZZZZZ").Should().BeNull();
    }

    [Test]
    public async Task Help_messages_belong_to_a_step_and_go_live()
    {
        var (w, id, _, _) = await NewFigure(NewWorkshop());
        w.SetText(id, Viewers.Master, "masterName", "Julius");
        var c = w.AddComment(id, Viewers.Master, "class", "Try a ranger – you like bows.")!;
        (c.Step, c.FromName).Should().Be(("class", "Julius"));
        w.AddComment(id, Viewers.Player, "nonsense", "ok")!.Step.Should().Be("start");
        w.AddComment(id, Viewers.Player, "class", "  ").Should().BeNull();
        _live.Sent.Should().Contain(s => s.Kind == "kommentar");
    }

    [Test]
    public async Task A_customer_finds_their_figures_with_the_customer_number()
    {
        var w = NewWorkshop();
        var (o, _, _) = await w.CreateAsync("", "mystery", "1111 2222", "ip", default);
        w.View(o!.FigureId, Viewers.Player)!.PlayerName.Should().Be("Julius");
        w.View(o.FigureId, Viewers.Player)!.Steps.Should().NotContain(Steps.Abilities);
        var (list, _, _) = await w.MineAsync("11112222", "ip", default);
        list.Should().ContainSingle(x => x.Id == o.FigureId);
        (await w.CreateAsync("", "dnd", "99998888", "ip", default)).Error.Should().Be(OpenError.Customer);
    }
}
