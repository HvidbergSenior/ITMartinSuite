using System.Net;
using System.Threading.Channels;
using FluentAssertions;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

namespace ITMartinSnak.Tests;

// Idéer (2026-11-20): paste a link or a text from a mail or a web page - it becomes a card that can turn into an Opgave.
public class IdeaTests
{
    private string _dir = "";
    private readonly List<string> _kinds = [];
    private readonly List<string> _pushed = [];

    private sealed class Live(List<string> kinds) : ILiveFeed
    {
        public void Send(string room, string kind, object data) => kinds.Add(kind);
        public (Guid Id, ChannelReader<string> Reader) Join(string room) => (Guid.NewGuid(), Channel.CreateUnbounded<string>().Reader);
        public void Leave(Guid id) { }
    }

    private sealed class Push(List<string> sent) : IPushSender
    {
        public string PublicKey => "test";
        public void Subscribe(string room, string endpoint, string p256dh, string auth, string name, bool host = false) { }
        public void Unsubscribe(string endpoint) { }
        public void SendToOthers(string room, PushFrom? from, string title, string body, string tag, string url) => sent.Add(body);
        public void ForgetRoom(string room) { }
        public IReadOnlyDictionary<string, int> PhonesPerName(string room) => new Dictionary<string, int>();
    }

    private sealed class Titles(string? title) : IPageTitles
    {
        public int Calls;
        public Task<string?> TitleAsync(string url, CancellationToken ct = default) { Calls++; return Task.FromResult(title); }
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "snak-ide-" + Guid.NewGuid().ToString("N"));
        _kinds.Clear(); _pushed.Clear();
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

    private Group NewGroup() => new(new JsonGroupStore(_dir), new Live(_kinds), new Push(_pushed), TimeProvider.System,
        new RoomContext("abc", "Loppemarked", "/r/x", _ => { }));

    [Test]
    public void A_pasted_mail_gets_its_first_line_as_title_and_the_rest_as_text()
    {
        var p = IdeaRules.Parse("Stadeplads i Ceres Park søndag\r\n\r\nPris 150 kr, tilmelding senest fredag.\nhttps://loppemarked.dk/ceres.")!.Value;
        p.Title.Should().Be("Stadeplads i Ceres Park søndag");
        p.Text.Should().Be("Pris 150 kr, tilmelding senest fredag.");
        p.Url.Should().Be("https://loppemarked.dk/ceres");
    }

    [Test]
    public void Only_a_link_uses_the_page_title_or_else_the_site_name()
    {
        IdeaRules.NeedsTitle("https://www.lions.dk/aarhus").Should().BeTrue();
        IdeaRules.NeedsTitle("Lions Aarhus\nhttps://www.lions.dk/aarhus").Should().BeFalse();
        IdeaRules.Parse("https://www.lions.dk/aarhus", "Lions Club   Aarhus")!.Value.Title.Should().Be("Lions Club Aarhus");
        IdeaRules.Parse("https://www.lions.dk/aarhus")!.Value.Title.Should().Be("www.lions.dk");
        IdeaRules.Parse("   ").Should().BeNull();
        IdeaRules.FirstUrl("se (https://a.dk/x).").Should().Be("https://a.dk/x");
        IdeaRules.FirstUrl("javascript:alert(1) ftp://x").Should().BeNull();
    }

    [Test]
    public void A_long_first_line_is_cut_at_a_word_and_kept_whole_in_the_text()
    {
        var line = string.Join(" ", Enumerable.Repeat("ordet", 40));
        var p = IdeaRules.Parse(line)!.Value;
        p.Title.Length.Should().BeLessThanOrEqualTo(IdeaRules.MaxTitle + 1);
        p.Title.Should().EndWith("…");
        p.Text.Should().Be(line);
    }

    [Test]
    public async Task Ideas_are_saved_shown_live_pushed_and_survive_a_restart()
    {
        var titles = new Titles("Digitalt loppemarked – idé");
        var intake = new IdeaIntake(titles);
        var (idea, err) = await intake.AddAsync(NewGroup(), "Lukas", "https://example.dk/side", new PushFrom("phone-1"));
        err.Should().BeNull();
        idea!.Title.Should().Be("Digitalt loppemarked – idé");
        titles.Calls.Should().Be(1);
        _kinds.Should().Contain("ideer");
        _pushed.Should().ContainSingle().Which.Should().Be("Lukas: Digitalt loppemarked – idé");

        await intake.AddAsync(NewGroup(), "Lukas", "Prutning\nTag 4 – få dem billigere", null);
        titles.Calls.Should().Be(1);   // a text with its own title fetches nothing

        NewGroup().Ideas().Select(i => i.Title).Should().Equal("Prutning", "Digitalt loppemarked – idé");
    }

    [Test]
    public void A_name_is_needed_and_only_the_one_who_added_it_or_the_host_deletes()
    {
        var g = NewGroup();
        g.AddIdea("", "Idé").Error.Should().Be("Skriv dit navn først.");
        var (idea, _) = g.AddIdea("Lukas", "Idé");
        g.DeleteIdea(idea!.Id, "Martin", host: false).Should().BeFalse();
        g.DeleteIdea(idea.Id, "lukas", host: false).Should().BeTrue();
        g.Ideas().Should().BeEmpty();
    }

    [Test]
    public void An_idea_becomes_a_task_once_with_its_text_and_link_as_the_note()
    {
        var g = NewGroup();
        var (idea, _) = g.AddIdea("Lukas", "Bod på Ceres\nSøndag kl 8\nhttps://loppemarked.dk/ceres");
        var t = g.IdeaToTask(idea!.Id);
        t!.Title.Should().Be("Bod på Ceres");
        t.Note.Should().Contain("Søndag kl 8").And.Contain("https://loppemarked.dk/ceres");
        g.Ideas().Single().TaskId.Should().Be(t.Id);
        g.IdeaToTask(idea.Id).Should().BeNull();   // not twice
        g.Tasks().Should().ContainSingle();
    }

    [Test]
    public void Page_titles_are_read_from_og_title_or_the_title_tag()
    {
        HttpPageTitles.TitleIn("<html><head><title> Lions &amp; venner </title></head>").Should().Be("Lions & venner");
        HttpPageTitles.TitleIn("<meta property=\"og:title\" content=\"Det rigtige navn\"><title>x</title>").Should().Be("Det rigtige navn");
        HttpPageTitles.TitleIn("<p>ingen titel</p>").Should().BeNull();
    }

    [TestCase("10.0.0.126", false)]
    [TestCase("10.0.0.200", false)]
    [TestCase("127.0.0.1", false)]
    [TestCase("192.168.1.1", false)]
    [TestCase("172.20.0.5", false)]
    [TestCase("169.254.169.254", false)]
    [TestCase("100.117.120.44", false)]   // Tailscale
    [TestCase("::1", false)]
    [TestCase("fd00::1", false)]
    [TestCase("::ffff:10.0.0.1", false)]
    [TestCase("93.184.216.34", true)]
    [TestCase("2a00:1450:4001::1", true)]
    public void Only_public_addresses_are_ever_fetched(string ip, bool ok) =>
        HttpPageTitles.IsPublic(IPAddress.Parse(ip)).Should().Be(ok);
}
