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


public class CustomerPollTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_customer_poll_needs_a_question_and_two_choices_and_never_asks_for_emails()
    {
        CustomerPollRules.Make("48217730", " ", "", ["a", "b"], false, null, [], Now).Error.Should().Contain("handler om");
        CustomerPollRules.Make("48217730", "Sommerfest?", "", ["Have", " ", "have"], false, null, [], Now).Error.Should().Contain("mindst 2");
        CustomerPollRules.Make("48217730", "Sommerfest?", "", ["Have", "Hal"], false, Now.AddHours(-1), [], Now).Error.Should().Contain("fremtiden");
        CustomerPollRules.Make("48217730", "Sommerfest?", "", ["Have", "Hal"], false, null, [QuestionKinds.Choose], Now).Error.Should().Be(CustomerPollRules.FreeLimit(QuestionKinds.Choose));   // free = one open of each kind
        CustomerPollRules.Make("48217730", "Sommerfest?", "", ["Have", "Hal"], false, null, [QuestionKinds.Days, QuestionKinds.Grade], Now).Error.Should().BeNull();   // other kinds open: fine

        var (error, poll) = CustomerPollRules.Make("48217730", " Sommerfest? ", "", ["Have", "Hal", ""], true, null, [], Now);
        error.Should().BeNull();
        (poll!.Title, poll.OwnerCode, poll.RequireContact, poll.AllowMultiple, poll.Options.Count).Should().Be(("Sommerfest?", "48217730", false, true, 2));
        poll.Deadline.Should().Be(Now.AddDays(14));   // free: closes by itself after 14 days
        CustomerPollRules.Make("48217730", "Q", "", ["a", "b"], false, Now.AddDays(90), [], Now).Poll!.Deadline.Should().Be(Now.AddDays(14));
        CustomerPollRules.Make("48217730", "Q", "", ["a", "b"], false, Now.AddDays(5), [], Now).Poll!.Deadline.Should().Be(Now.AddDays(5));
    }

    [Test]
    public void The_kundenummer_is_typed_with_or_without_the_space() =>
        CustomerPolls.Normalize(" 4821 7730 ").Should().Be("48217730");
}

public class QuestionKindTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Voter Mads = new("Mads", "");

    private static Poll Q(bool multi = false, string kind = QuestionKinds.Choose) => new()
    {
        Id = 1, Kind = kind, AllowMultiple = multi, Options = [new PollOption { Id = 10, Label = "A" }, new PollOption { Id = 11, Label = "B" }],
    };

    [Test]
    public void Choose_several_keeps_each_and_a_second_tap_takes_it_back()
    {
        var p = Q(multi: true);
        PackageRules.Choose(p, Mads, 10, "", Now);
        PackageRules.Choose(p, Mads, 11, "", Now);
        p.Votes.Select(v => v.OptionId).Should().Equal(10, 11);
        PackageRules.Choose(p, Mads, 10, "", Now);
        p.Votes.Select(v => v.OptionId).Should().Equal(11);

        var one = Q();
        PackageRules.Choose(one, Mads, 10, "", Now);
        PackageRules.Choose(one, Mads, 11, "", Now);
        one.Votes.Should().ContainSingle().Which.OptionId.Should().Be(11);   // one answer: choosing again changes it
    }

    [Test]
    public void Grades_are_1_to_5_per_answer_with_a_comment_on_each()
    {
        var p = Q(kind: QuestionKinds.Grade);
        PackageRules.Grade(p, Mads, 10, 4, Now);
        PackageRules.Grade(p, Mads, 11, 9, Now);   // out of range: ignored
        PackageRules.OptionComment(p, Mads, 11, "for dyr", Now).Should().BeTrue();
        p.Votes.Should().HaveCount(2);
        p.Votes.Single(v => v.OptionId == 10).Score.Should().Be(4);
        p.Votes.Single(v => v.OptionId == 11).Should().Match<Vote>(v => v.Score == 0 && v.Comment == "for dyr");

        PackageRules.Grade(p, Mads, 11, 2, Now);
        PackageRules.Grade(p, Mads, 11, 2, Now);   // same again takes the grade back, the comment stays
        p.Votes.Single(v => v.OptionId == 11).Should().Match<Vote>(v => v.Score == 0 && v.Comment == "for dyr");
    }

    [Test]
    public void Days_are_the_chosen_weekdays_in_the_period()
    {
        var from = new DateOnly(2026, 10, 21);   // a Wednesday
        DayRules.Days(from, from.AddMonths(3), DayRules.Weekend).Should().HaveCount(26).And.StartWith("lør 24. okt");
        DayRules.Days(from, from.AddDays(13), [DayOfWeek.Tuesday]).Should().Equal("tir 27. okt", "tir 3. nov");
        DayRules.Days(from, from.AddMonths(6), Enum.GetValues<DayOfWeek>()).Should().HaveCount(QuestionKinds.MaxDays);
    }

    [Test]
    public void Day_labels_turn_back_into_dates_across_new_year()
    {
        var made = new DateOnly(2026, 10, 21);
        var labels = DayRules.Days(made, new DateOnly(2027, 2, 1), DayRules.Weekend);
        var dates = DayRules.Dates(labels, made);
        dates.Should().HaveCount(labels.Count).And.NotContainNulls();
        dates[0].Should().Be(new DateOnly(2026, 10, 24));
        dates[^1].Should().Be(new DateOnly(2027, 1, 31));
        DayRules.Dates(["Fyn", "lør 9. jan"], made).Should().Equal(null, new DateOnly(2027, 1, 9));
        DayRules.MonthHeading(new DateOnly(2027, 1, 9)).Should().Be("januar 2027");
    }

    [Test]
    public void Several_questions_become_one_link_answered_with_a_name()
    {
        var (error, pkg) = CustomerPollRules.MakeSurvey("48217730", "FezBoyz", "", [
            new QuestionDraft("Hvor?", ["Fyn", "Århus", "Vestpå"], true),
            new QuestionDraft("Hvilket mærke er pænest?", ["A", "B"], false, QuestionKinds.Grade),
            new QuestionDraft("Hvilke dage?", DayRules.Days(new DateOnly(2026, 10, 21), new DateOnly(2027, 1, 21), DayRules.Weekend), false, QuestionKinds.Days),
        ], null, [], Now);
        error.Should().BeNull();
        pkg!.OpenByLink.Should().BeTrue();
        pkg.Polls.Select(p => p.Kind).Should().Equal(QuestionKinds.Choose, QuestionKinds.Grade, QuestionKinds.Days);
        pkg.Polls[2].AllowMultiple.Should().BeTrue();   // you can be free on several days
        pkg.Polls.Should().OnlyContain(p => p.Deadline == Now.AddDays(14) && p.OwnerCode == "48217730");

        CustomerPollRules.MakeSurvey("48217730", "X", "", [new QuestionDraft("", ["a", "b"], false)], null, [], Now).Error.Should().Contain("Skriv spørgsmål 1");
        CustomerPollRules.MakeSurvey("48217730", "X", "", [new QuestionDraft("Q", ["a", "b"], false), new QuestionDraft("Dage?", ["lør 7. nov", "søn 8. nov"], true, QuestionKinds.Days)], null, [QuestionKinds.Days], Now)
            .Error.Should().Be(CustomerPollRules.FreeLimit(QuestionKinds.Days));   // a link takes every kind it asks
    }
}

// Stem for customers, 2026-10-28: one open of each kind, pictures on answers, no name needed on a link,
// a comment on each chosen answer and a star for the first priority.
public class CustomerExtrasTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 18, 0, 0, DateTimeKind.Utc);
    private const string Pic = "c-0123456789abcdef0123456789abcdef.jpg";

    private static Poll Q(bool multi = true, string kind = QuestionKinds.Choose) => new()
    {
        Id = 1, Kind = kind, AllowMultiple = multi,
        Options = [new PollOption { Id = 10, Label = "A" }, new PollOption { Id = 11, Label = "B" }, new PollOption { Id = 12, Label = "C" }],
    };

    [Test]
    public void Pictures_stay_with_their_answer_and_a_picture_alone_is_an_answer()
    {
        var (error, poll) = CustomerPollRules.Make("48217730", "Hvilken sofa?", "", ["Rød", "", "  ", "Grøn"], false, null, [], Now,
            images: [Pic, null, Pic.Replace('0', '1'), null]);
        error.Should().BeNull();
        poll!.Options.Select(o => (o.Label, o.ImageName)).Should().Equal(("Rød", Pic), ("Billede 3", Pic.Replace('0', '1')), ("Grøn", null));
    }

    [TestCase("c-0123456789abcdef0123456789abcdef.jpg", true)]
    [TestCase("0123456789abcdef0123456789abcdef.jpg", true)]
    [TestCase("../../etc/passwd", false)]
    [TestCase("c-0123456789abcdef0123456789abcdef.svg", false)]
    [TestCase(null, false)]
    public void Only_names_the_picture_store_made_are_kept(string? name, bool ok) => CustomerPollRules.IsStoredImage(name).Should().Be(ok);

    [Test]
    public void Days_never_carry_pictures() =>
        CustomerPollRules.Make("48217730", "Dage?", "", ["lør 7. nov", "søn 8. nov"], true, null, [], Now, QuestionKinds.Days, [Pic, Pic])
            .Poll!.Options.Should().OnlyContain(o => o.ImageName == null);

    [Test]
    public void Without_a_name_each_phone_keeps_its_own_answers()
    {
        var p = Q(multi: false);
        PackageRules.Choose(p, new Voter("", "phone-1"), 10, "", Now);
        PackageRules.Choose(p, new Voter("", "phone-2"), 11, "", Now);
        PackageRules.Choose(p, new Voter("", "phone-1"), 12, "", Now);   // phone 1 changes its mind
        p.Votes.Select(v => (v.VoterToken, v.OptionId)).Should().BeEquivalentTo([("phone-1", 12), ("phone-2", 11)]);
        p.Votes.Select(Voter.Key).Distinct().Should().HaveCount(2);
        new Voter("", "").Owns(p.Votes[0]).Should().BeFalse();   // no name and no phone = nobody's answers
        PackageRules.IsIdentified("", "", needsHouse: false).Should().BeTrue();
        PackageRules.IsIdentified("Martin", "", needsHouse: true).Should().BeFalse();   // the husstand packages still need both
    }

    [Test]
    public void A_comment_goes_on_a_chosen_answer_and_leaves_with_it()
    {
        var p = Q();
        var me = new Voter("Mads", "");
        PackageRules.OptionComment(p, me, 10, "hvis det er billigt", Now).Should().BeFalse();   // not chosen yet
        PackageRules.Choose(p, me, 10, "", Now);
        PackageRules.Choose(p, me, 11, "", Now);
        PackageRules.OptionComment(p, me, 10, "hvis det er billigt", Now).Should().BeTrue();
        p.Votes.Single(v => v.OptionId == 10).Comment.Should().Be("hvis det er billigt");
        PackageRules.Choose(p, me, 10, "", Now);   // taken back
        p.Votes.Should().ContainSingle().Which.Comment.Should().BeEmpty();
    }

    [Test]
    public void One_chosen_answer_can_have_the_star()
    {
        var p = Q();
        var me = new Voter("Mads", "");
        PackageRules.Choose(p, me, 10, "", Now);
        PackageRules.Choose(p, me, 11, "", Now);
        PackageRules.SetFirst(p, me, 12);   // not chosen: nothing
        p.Votes.Should().OnlyContain(v => v.Priority == 0);
        PackageRules.SetFirst(p, me, 10);
        PackageRules.SetFirst(p, me, 11);   // the star moves
        p.Votes.Single(v => v.Priority == 1).OptionId.Should().Be(11);
        PackageRules.FirstCount(p, 11).Should().Be(1);
        PackageRules.SetFirst(p, me, 11);   // again: taken back
        p.Votes.Should().OnlyContain(v => v.Priority == 0);

        PackageRules.SetFirst(p, me, 10);
        PackageRules.Choose(p, me, 11, "", Now);   // only one left: no star
        p.Votes.Should().ContainSingle().Which.Priority.Should().Be(0);

        var single = Q(multi: false);
        PackageRules.Choose(single, me, 10, "", Now);
        PackageRules.SetFirst(single, me, 10);
        single.Votes.Single().Priority.Should().Be(0);   // one answer only: a star means nothing
    }

    [Test]
    public void A_single_poll_takes_a_star_and_a_comment_per_answer()
    {
        var ballot = new Ballot(1, "Q", "", true, false, true, null,
            [new BallotOption(10, "A", 0), new BallotOption(11, "B", 1)], []);
        var (error, vote) = VoteRules.Accept(ballot, new VoteRequest([10, 11], "Mads", null, "", false, 11,
            new Dictionary<int, string> { [10] = " fin ", [12] = "not chosen" }), Now);
        error.Should().BeNull();
        vote!.Rows.Select(r => (r.OptionId, r.Priority, r.Comment)).Should().Equal((10, 0, "fin"), (11, 1, ""));

        var one = VoteRules.Accept(ballot, new VoteRequest([10], "Mads", null, "", false, 10), Now).Vote!;
        one.Rows.Single().Priority.Should().Be(0);   // a star needs two answers
    }
}
