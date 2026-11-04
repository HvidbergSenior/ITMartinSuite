using System.Text.Json;
using FalsigForening.Application;
using FalsigForening.Domain;
using FalsigForening.Infrastructure.External;
using FluentAssertions;
using NUnit.Framework;

namespace FalsigForening.Tests.Domain;

public class ForeningRulesTests
{
    private static ForeningProfil Valid() => new() { GroupId = Guid.NewGuid(), Cvr = "12345678", Medlemstal = 40, MedlemmerUnder25 = 10 };

    [Test]
    public void Good_details_have_no_problems() => ForeningRules.Problems("Borgerforeningen", Valid()).Should().BeEmpty();

    [TestCase("1234 5678", "12345678")]
    [TestCase("1234567", "")]
    [TestCase("1234567a", "")]
    [TestCase("", "")]
    public void A_cvr_number_is_eight_digits(string typed, string clean) => ForeningRules.CleanCvr(typed).Should().Be(clean);

    [Test]
    public void Every_problem_is_named_in_danish()
    {
        var p = Valid();
        p.Cvr = "123";
        p.MedlemmerUnder25 = 50;
        p.Email = "ikke-en-mail";
        p.GeneralforsamlingMd = 13;
        ForeningRules.Problems("", p).Should().HaveCount(5);
    }

    [TestCase("foreningen.dk", "https://foreningen.dk")]
    [TestCase("http://foreningen.dk", "http://foreningen.dk")]
    [TestCase("  ", "")]
    public void A_website_gets_https(string typed, string clean) => ForeningRules.CleanWebsite(typed).Should().Be(clean);

    [Test]
    public void Cvr_fills_the_number_and_date_but_never_overwrites_a_typed_mail()
    {
        var p = new ForeningProfil { Email = "bestyrelsen@forening.dk" };
        p.FillFromCvr(new CvrDetails("87654321", "Husby Borgerforening", "Vej 1", "6990", "Ulfborg", new DateOnly(1974, 8, 1), "Forening", "cvr@forening.dk", false));
        p.Cvr.Should().Be("87654321");
        p.CvrRegistreret.Should().Be(new DateOnly(1974, 8, 1));
        p.Email.Should().Be("bestyrelsen@forening.dk");
    }

    [Test]
    public void Every_group_but_the_demo_is_an_association_board()
    {
        ClubGroups.IsBoard("husby-borgerforening").Should().BeTrue();
        ClubGroups.IsBoard("demo").Should().BeFalse();
    }
}

public class CvrApiTests
{
    [Test]
    public void Reads_the_public_details_and_the_danish_start_date()
    {
        using var doc = JsonDocument.Parse("""{"vat":12345678,"name":"Husby Borgerforening","address":"Vej 1","zipcode":"6990","city":"Ulfborg","startdate":"01/08 - 1974","companydesc":"Forening","email":null,"enddate":null,"addressco":"Hemmelig Person"}""");
        var d = CvrApiLookup.Parse(doc)!;
        d.Cvr.Should().Be("12345678");
        d.Name.Should().Be("Husby Borgerforening");
        d.StartDate.Should().Be(new DateOnly(1974, 8, 1));
        d.Ceased.Should().BeFalse();
    }

    [Test]
    public void An_error_answer_is_not_found()
    {
        using var doc = JsonDocument.Parse("""{"error":"NOT_FOUND"}""");
        CvrApiLookup.Parse(doc).Should().BeNull();
    }
}

public class ForeningProfileTests
{
    [Test]
    public async Task Only_an_admin_can_save()
    {
        var group = new Group { Name = "Gammelt navn" };
        var (use, _) = Make(group);
        var member = new Member { GroupId = group.Id, Role = "Medlem" };
        var r = await use.SaveAsync(member, "Nyt navn", ForeningProfil.NewFor(group.Id));
        r.Saved.Should().BeFalse();
        group.Name.Should().Be("Gammelt navn");
    }

    [Test]
    public async Task An_admin_saves_the_name_and_the_details_once()
    {
        var group = new Group { Name = "Gammelt navn" };
        var (use, items) = Make(group);
        var admin = new Member { GroupId = group.Id, Role = "Admin" };
        var details = ForeningProfil.NewFor(group.Id);
        details.Cvr = "1234 5678";
        details.Hjemmeside = "forening.dk";

        (await use.SaveAsync(admin, "Husby Borgerforening", details)).Saved.Should().BeTrue();
        (await use.SaveAsync(admin, "Husby Borgerforening", details)).Saved.Should().BeTrue();

        group.Name.Should().Be("Husby Borgerforening");
        items.Rows.Should().ContainSingle().Which.Cvr.Should().Be("12345678");
        items.Rows[0].Hjemmeside.Should().Be("https://forening.dk");
    }

    private static (ForeningProfile, FakeItems) Make(Group group)
    {
        var items = new FakeItems();
        return (new ForeningProfile(new FakeTeams(group), items, new NoCvr(), TimeProvider.System), items);
    }

    private sealed class NoCvr : ICvrLookup
    {
        public Task<CvrDetails?> LookupAsync(string numberOrName, CancellationToken ct = default) => Task.FromResult<CvrDetails?>(null);
    }

    private sealed class FakeItems : ITeamItems<ForeningProfil>
    {
        public List<ForeningProfil> Rows { get; } = [];
        public Task<List<ForeningProfil>> ListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(Rows.Where(r => r.GroupId == groupId).ToList());
        public Task<ForeningProfil?> GetAsync(Guid groupId, Guid id, CancellationToken ct = default) => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));
        public Task AddAsync(ForeningProfil item, CancellationToken ct = default) { Rows.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(ForeningProfil item, CancellationToken ct = default)
        {
            Rows.RemoveAll(r => r.Id == item.Id);
            Rows.Add(item);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(IEnumerable<ForeningProfil> items, CancellationToken ct = default) => Task.WhenAll(items.Select(i => UpdateAsync(i, ct)));
        public Task DeleteAsync(Guid groupId, Guid id, CancellationToken ct = default) { Rows.RemoveAll(r => r.Id == id); return Task.CompletedTask; }
    }

    // Only the two calls the use case makes; the rest is never reached in these tests.
    private sealed class FakeTeams(Group group) : ITeamStore
    {
        public Task<Group?> GroupAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Group?>(id == group.Id ? group : null);
        public Task UpdateGroupAsync(Group g, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Group?> GroupAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddGroupAsync(Group g, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Member>> MembersAsync(Guid groupId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Member?> MemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Member?> MemberByNameAsync(Guid groupId, string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Member?> MemberByLoginTokenAsync(string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Member?> FirstWithRoleAsync(string slug, string role, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMemberAsync(Member member, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MemberSession>> SessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddSessionAsync(MemberSession session, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetSessionExpiryAsync(Guid sessionId, DateTime expiresAt, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
