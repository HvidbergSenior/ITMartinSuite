using FluentAssertions;
using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure;
using ITMartinPoll.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Tests;

public class VoteRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BallotOption[] Opts = [new(1, "Anders And-blade", 0), new(2, "Tintin", 1), new(3, "Jumbobøger", 2)];

    private static Ballot Poll(bool multi = true, bool contact = true, bool active = true, DateTime? deadline = null, params CastVote[] votes) =>
        new(7, "Første liveauktion", "", multi, contact, active, deadline, Opts, votes);

    [Test]
    public void Contact_poll_needs_name_and_a_real_email_and_only_one_vote_per_email()
    {
        VoteRules.Accept(Poll(), new([1], "", "a@b.dk", null, false), Now).Error.Should().Be("Skriv dit navn.");
        VoteRules.Accept(Poll(), new([1], "Ole", "ole@gmail", null, false), Now).Error.Should().StartWith("Skriv en rigtig e-mail");
        var voted = Poll(votes: new CastVote(1, "Ole", "ole@gmail.com", Now));
        VoteRules.Accept(voted, new([2], "Ole", " OLE@gmail.com ", null, false), Now).Error.Should().Contain("allerede stemt");
        VoteRules.Accept(Poll(), new([1, 3], "Ole", "ole@gmail.com", "Leder efter 1950'erne", false), Now).Error.Should().BeNull();
    }

    [Test]
    public void Closed_or_expired_polls_take_no_votes_and_single_choice_takes_one()
    {
        VoteRules.Accept(Poll(active: false), new([1], "Ole", "ole@gmail.com", null, false), Now).Error.Should().Be("Afstemningen er lukket.");
        VoteRules.Accept(Poll(deadline: Now.AddMinutes(-1)), new([1], "Ole", "ole@gmail.com", null, false), Now).Error.Should().Be("Afstemningen er lukket.");
        VoteRules.Accept(Poll(multi: false), new([1, 2], "Ole", "ole@gmail.com", null, false), Now).Error.Should().Be("Du kan kun vælge én mulighed.");
        VoteRules.Accept(Poll(), new([99], "Ole", "ole@gmail.com", null, false), Now).Error.Should().Be("Vælg mindst én mulighed.");
    }

    [Test]
    public void Opt_in_only_when_ticked_and_keeps_the_choices_as_interests()
    {
        var (_, no) = VoteRules.Accept(Poll(), new([1], "Ole", "ole@gmail.com", null, false), Now);
        no!.OptIn.Should().BeNull();
        var (_, yes) = VoteRules.Accept(Poll(), new([3, 1], "Ole Hansen", "ole@gmail.com", "har du Fantomet?", true), Now);
        yes!.OptIn!.Interests.Should().Be("Anders And-blade, Jumbobøger | har du Fantomet?");
        yes.OptIn.ConsentText.Should().Be(VoteRules.OptInText);
        yes.Rows.Should().HaveCount(2);
    }

    [Test]
    public void Open_poll_without_contact_never_stores_an_email_and_counts_people_not_rows()
    {
        var (_, v) = VoteRules.Accept(Poll(contact: false), new([1, 2], "", "x@y.dk", null, true), Now);
        v!.Rows.Should().OnlyContain(r => r.Email == "");
        v.OptIn.Should().BeNull();
        VoteRules.People([new(1, "", "a@b.dk", Now), new(2, "", "a@b.dk", Now), new(1, "", "c@d.dk", Now)]).Should().Be(2);
    }
}

public class VotingStoreTests
{
    private SqliteConnection _conn = null!;
    private Voting _voting = null!;
    private readonly FakeKunder _kunder = new();

    private sealed class Factory(DbContextOptions<PollDb> o) : IDbContextFactory<PollDb>
    {
        public PollDb CreateDbContext() => new(o);
    }

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<PollDb>().UseSqlite(_conn).Options;
        using (var db = new PollDb(options))
        {
            db.Database.EnsureCreated();   // the real tables, as poll.db has them
            db.Polls.Add(new Poll { Title = "Første liveauktion", Body = "Hvad skal på?", CreatedAt = DateTime.UtcNow, IsActive = true, AllowMultiple = true, RequireContact = true,
                Options = [new PollOption { Label = "Anders And-blade", SortOrder = 0 }, new PollOption { Label = "Tintin", SortOrder = 1 }] });
            db.SaveChanges();
        }
        _voting = new Voting(new VotingStore(new Factory(options)), _kunder, TimeProvider.System);
    }

    [TearDown] public void TearDown() => _conn.Dispose();

    [Test]
    public async Task Vote_round_trip_counts_people_and_saves_the_opt_in_once()
    {
        var view = await _voting.ViewAsync(1);
        view!.Options.Select(o => o.Label).Should().Equal("Anders And-blade", "Tintin");

        var (err, after) = await _voting.VoteAsync(1, new([1, 2], "Ole", "ole@gmail.com", "tak", true));
        err.Should().BeNull();
        after!.People.Should().Be(1);
        after.Options.Should().OnlyContain(o => o.People == 1);

        (await _voting.VoteAsync(1, new([1], "Ole", "ole@gmail.com", null, false))).Error.Should().Contain("allerede stemt");

        _kunder.OptIns.Should().ContainSingle().Which.Interests.Should().Be("Anders And-blade, Tintin | tak");   // to Kunder, once
    }
}

// Kunder (the Kundekartotek since 2026-10-21) as the use cases see it.
internal sealed class FakeKunder : ICustomerStore
{
    public List<CustomerOptIn> OptIns = [];
    public List<string> Unsubscribed = [];
    public List<string> Moved = [];
    public Task UpsertAsync(CustomerOptIn o, CancellationToken ct = default) { OptIns.Add(o); return Task.CompletedTask; }
    public Task UnsubscribeAsync(string email, CancellationToken ct = default) { Unsubscribed.Add(email); return Task.CompletedTask; }
    public Task<(bool Found, string Email)> UnsubscribeByLinkAsync(string token, CancellationToken ct = default) => Task.FromResult((true, "x@y.dk"));
    public Task MoveAsync(Customer old, CancellationToken ct = default) { Moved.Add(old.Email); return Task.CompletedTask; }
}

public class CustomerListTests
{
    [Test]
    public async Task Sign_up_checks_the_email_and_passes_it_on_to_Kunder_and_afmeld_takes_just_the_email()
    {
        var kunder = new FakeKunder();
        var list = new CustomerList(kunder, TimeProvider.System);

        (await list.SignUpAsync("ikke-en-mail", "Ole", "x", "y", "z")).Should().NotBeNull();
        (await list.SignUpAsync(" Ole@Gmail.com", "Ole", "itmartin.dk: Prøv gratis – Tilbud", "consent", "Tilbud")).Should().BeNull();
        kunder.OptIns.Should().ContainSingle().Which.Email.Should().Be("ole@gmail.com");
        await list.UnsubscribeAsync(" OLE@gmail.com");
        kunder.Unsubscribed.Should().Equal("ole@gmail.com");
        (await list.UnsubscribeByLinkAsync("kort")).Found.Should().BeFalse();
        CustomerRules.CompanyOf("itmartin.dk: Prøv gratis – Tilbud").Should().Be("ITMartin");
        CustomerRules.CompanyOf("Stem: Første liveauktion").Should().Be("Bogshoppen");
    }
}

