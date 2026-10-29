using FluentAssertions;
using ITMartinFaellesskab.Application;
using ITMartinFaellesskab.Domain;
using ITMartinFaellesskab.Infrastructure;

namespace ITMartinFaellesskab.Tests;

// The communities (Application) with the real JSON files (Infrastructure) and fakes for Kunder and the tip APIs.
public class CommunityTests
{
    private string _dir = "";
    private Communities _c = null!;

    private sealed class FakeKunder(bool ok, bool reached = true, string name = "") : ICustomerCheck
    {
        public int Calls;
        public Task<(bool Ok, bool Reached, string Name, string Error)> CheckAsync(string code, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult((ok, reached, name, ok ? "" : reached ? "Det kundenummer virker ikke." : "Vi kan ikke tjekke dit kundenummer lige nu."));
        }
    }

    private sealed class FakeTips(TipKind kind) : ITipSource
    {
        public List<string> Queries = [];
        public bool Fail;
        public TipKind Kind => kind;
        public Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default)
        {
            Queries.Add(query);
            if (Fail) throw new HttpRequestException("down");
            return Task.FromResult(Enumerable.Range(1, 9).Select(i => new Tip($"{query} {i}", "", "https://x/" + i, "")).ToList());
        }
    }

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fk-test-" + Guid.NewGuid().ToString("N"));
        _c = new Communities(new JsonCommunityStore(_dir), TimeProvider.System);
    }

    [TearDown]
    public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Test]
    public void Every_interest_the_user_named_has_a_community()
    {
        CommunityRules.All.Select(c => c.Slug).Should().Contain(["boeger", "anders-and", "blade", "magic", "braetspil", "pc-spil", "videoaften", "musik"]);
        CommunityRules.All.Select(c => c.Slug).Should().OnlyHaveUniqueItems();
        CommunityRules.Find("videoaften")!.Activity.Should().Contain("5 dvd").And.NotContain("lej");
    }

    [Test]
    public void Kundenummer_is_eight_digits_with_or_without_space()
    {
        CommunityRules.NormaliseCode("4821 7730").Should().Be("48217730");
        CommunityRules.NormaliseCode("1234").Should().BeNull();
        CommunityRules.NormaliseCode(null).Should().BeNull();
    }

    [Test]
    public void Only_a_remembered_customer_can_write_and_messages_survive_a_restart()
    {
        _c.Write("11112222", "boeger", "hej").Should().BeNull();
        _c.Remember("11112222", "Rico", "");
        var m = _c.Write("11112222", "boeger", "  Har nogen læst Night Watch?  ");
        m!.Name.Should().Be("Rico");
        m.Text.Should().Be("Har nogen læst Night Watch?");
        _c.Write("11112222", "findes-ikke", "hej").Should().BeNull();

        var again = new Communities(new JsonCommunityStore(_dir), TimeProvider.System);
        again.MessagesAfter("boeger", 0).Should().ContainSingle().Which.Text.Should().Be("Har nogen læst Night Watch?");
        again.MessagesAfter("magic", 0).Should().BeEmpty();
    }

    [Test]
    public void A_typed_name_wins_and_the_Kunder_name_is_only_a_fallback()
    {
        _c.Remember("11112222", "", "Rico Hansen").Name.Should().Be("Rico Hansen");
        _c.Remember("11112222", "Rico", "Rico Hansen").Name.Should().Be("Rico");
        _c.Remember("11112222", "", "Rico Hansen").Name.Should().Be("Rico");
    }

    [Test]
    public void Join_toggles_and_is_counted()
    {
        _c.Remember("11112222", "Rico", "");
        _c.ToggleJoin("11112222", "magic")!.Slugs.Should().Equal("magic");
        _c.Get("magic", "11112222")!.Should().Match<CommunityView>(v => v.Joined && v.Members == 1);
        _c.ToggleJoin("11112222", "magic")!.Slugs.Should().BeEmpty();
        _c.Get("magic", null)!.Members.Should().Be(0);
        _c.ToggleJoin("99998888", "magic").Should().BeNull();
    }

    [Test]
    public void Right_now_replaces_the_old_one_and_needs_a_title()
    {
        _c.SetNow("boeger", "", "", "").Should().BeNull();
        _c.SetNow("boeger", "Night Watch", "", "fredag kl. 19");
        _c.SetNow("boeger", "Mort", "Døden får en lærling", "");
        _c.Get("boeger", null)!.Now!.Title.Should().Be("Mort");
        _c.ClearNow("boeger").Should().BeTrue();
        _c.Get("boeger", null)!.Now.Should().BeNull();
    }

    [Test]
    public async Task Tips_search_right_now_else_the_start_query_and_are_kept()
    {
        var books = new FakeTips(TipKind.Books);
        var tips = new Tips([books], _c, new TipCache(), TimeProvider.System);

        (await tips.ForAsync("boeger")).Should().HaveCount(6);
        books.Queries.Should().Equal("Terry Pratchett");

        _c.SetNow("boeger", "Mort", "", "");
        await tips.ForAsync("boeger");
        await tips.ForAsync("boeger");
        books.Queries.Should().Equal("Terry Pratchett", "Mort");   // the second "Mort" came from the cache

        (await tips.ForAsync("anders-and")).Should().BeEmpty();     // no API for Anders And - Martin's own tips
    }

    [Test]
    public async Task An_api_that_does_not_answer_gives_no_tips_instead_of_an_error()
    {
        var music = new FakeTips(TipKind.Music) { Fail = true };
        (await new Tips([music], _c, new TipCache(), TimeProvider.System).ForAsync("musik")).Should().BeEmpty();
    }

    [Test]
    public async Task Login_customer_admin_wrong_and_locked()
    {
        var guard = new LoginGuard();
        var good = new Access(new FakeKunder(true, name: "Rico"), _c, guard, "admin1234", TimeProvider.System);
        (await good.LoginAsync("admin1234", null, "ip")).Result.Should().Be(LoginResult.Admin);
        var r = await good.LoginAsync("1111 2222", null, "ip");
        r.Result.Should().Be(LoginResult.Customer);
        r.Code.Should().Be("11112222");
        _c.Me("11112222")!.Name.Should().Be("Rico");

        var bad = new Access(new FakeKunder(false), _c, guard, "admin1234", TimeProvider.System);
        for (var i = 0; i < Access.MaxWrongTries; i++) (await bad.LoginAsync("99998888", null, "ip2")).Result.Should().Be(LoginResult.Wrong);
        (await good.LoginAsync("1111 2222", null, "ip2")).Result.Should().Be(LoginResult.Locked);   // the guard is shared across requests
    }

    [Test]
    public async Task Kunder_not_answering_is_not_a_wrong_try()
    {
        var down = new Access(new FakeKunder(false, reached: false), _c, new LoginGuard(), "admin1234", TimeProvider.System);
        for (var i = 0; i < Access.MaxWrongTries + 2; i++)
            (await down.LoginAsync("11112222", null, "ip")).Result.Should().Be(LoginResult.Unreachable);
    }
}
