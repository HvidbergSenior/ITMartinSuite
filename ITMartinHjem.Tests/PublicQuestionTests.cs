using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinHjem.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Tests;

public class QuestionRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static PublicQuestion Q(string q, string a, string topic, int daysAgo = 0) =>
        PublicQuestion.Publish(q, a, topic, Now.AddDays(-daysAgo), null);

    [Test]
    public void Anonymize_removes_email_phone_and_the_visitors_name()
    {
        var t = QuestionRules.Anonymize("Hej, det er Jytte. Skriv til jytte@mail.dk eller ring 31 19 47 30", "Jytte");
        t.Should().NotContain("Jytte").And.NotContain("jytte@mail.dk").And.NotContain("31 19 47 30");
        t.Should().Contain("[skjult]");
    }

    [Test]
    public void Draft_takes_the_visitors_first_messages_as_question_and_Martins_replies_as_answer()
    {
        var (q, a) = QuestionRules.DraftFromChat(
            [(false, "Hej, jeg hedder Ole"), (false, "Virker Budget med Danske Bank?"), (true, "Ja, Ole – læg CSV-filen ind."), (false, "Tak!")], "Ole");
        q.Should().Be("Hej, jeg hedder\nVirker Budget med Danske Bank?");
        a.Should().Be("Ja, – læg CSV-filen ind.");
    }

    [Test]
    public void Publish_needs_question_and_answer_and_gives_a_topic()
    {
        var act = () => PublicQuestion.Publish(" ", "svar", "", Now, null);
        act.Should().Throw<ArgumentException>();
        PublicQuestion.Publish("Spørgsmål?", "Svar", "  budget ", Now, 3).Topic.Should().Be("Budget");
        PublicQuestion.Publish("Spørgsmål?", "Svar", "", Now, null).Topic.Should().Be(QuestionRules.NoTopic);
    }

    [Test]
    public void Search_needs_every_word_and_ranks_question_hits_first()
    {
        var a = Q("Virker Budget med Danske Bank?", "Ja, alle banker der kan lave CSV.", "Budget");
        var b = Q("Kan jeg se MobilePay?", "Ja, Budget viser MobilePay for sig.", "Budget");
        var c = Q("Hvad koster strøm nu?", "Se ElPriser.", "ElPriser");
        var all = new[] { a, b, c };

        QuestionRules.Search(all, "budget", null).Should().Equal(a, b);
        QuestionRules.Search(all, "budget mobilepay", null).Should().Equal(b);
        QuestionRules.Search(all, "", "elpriser").Should().Equal(c);
        QuestionRules.Search(all, "fodbold", null).Should().BeEmpty();
    }

    [Test]
    public void Related_finds_the_same_topic_and_shared_words_but_not_itself()
    {
        var a = Q("Virker Budget med Danske Bank?", "Ja.", "Budget");
        var b = Q("Kan Budget se MobilePay?", "Ja.", "Budget");
        var c = Q("Virker det med Nordea bank?", "Ja.", "Andet");
        var d = Q("Hvad koster strøm?", "Se ElPriser.", "ElPriser");
        SetId(a, 1); SetId(b, 2); SetId(c, 3); SetId(d, 4);

        QuestionRules.Related(a, [a, b, c, d]).Should().Equal(b, c);
    }

    [Test]
    public void Topics_are_counted_case_insensitive_most_asked_first()
    {
        var t = QuestionRules.Topics([Q("a?", "a", "Budget"), Q("b?", "b", "budget"), Q("c?", "c", "ElPriser")]);
        t.Should().Equal(("Budget", 2), ("ElPriser", 1));
    }

    [Test]
    public void Each_site_only_sees_its_own_questions()
    {
        var a = PublicQuestion.Publish("Hvornår er der liveauktion?", "Snart.", "Auktion", Now, null, "bogshoppen");
        var b = PublicQuestion.Publish("Virker Budget med Nordea?", "Ja.", "Budget", Now, null, null);
        a.Site.Should().Be(QuestionRules.Bogshoppen);
        b.Site.Should().Be(QuestionRules.ITMartin);
        QuestionRules.OnSite([a, b], "bogshoppen").Should().Equal(a);
        QuestionRules.OnSite([a, b], "itmartin").Should().Equal(b);
        QuestionRules.OnSite([a, b], null).Should().Equal(a, b);
    }

    private static void SetId(PublicQuestion q, int id) =>
        typeof(PublicQuestion).GetProperty(nameof(PublicQuestion.Id))!.SetValue(q, id);
}

public class PublicQuestionsUseCaseTests
{
    private SqliteConnection _conn = null!;
    private PublicQuestions _uc = null!;

    private sealed class Factory(DbContextOptions<HjemQuestionsDb> o) : IDbContextFactory<HjemQuestionsDb>
    {
        public HjemQuestionsDb CreateDbContext() => new(o);
    }

    [SetUp]
    public async Task SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<HjemQuestionsDb>().UseSqlite(_conn).Options;
        await using (var db = new HjemQuestionsDb(options)) await HjemQuestionsDb.EnsureTablesAsync(db);
        await using (var db = new HjemQuestionsDb(options)) await HjemQuestionsDb.EnsureTablesAsync(db);   // twice: idempotent
        _uc = new PublicQuestions(new PublicQuestionStore(new Factory(options)), TimeProvider.System);
    }

    [TearDown] public void TearDown() => _conn.Dispose();

    [Test]
    public async Task Publish_edit_search_and_remove_round_trip_through_sqlite()
    {
        var q = await _uc.PublishAsync(new("Virker Budget med Nordea? Ring 12345678", "Ja.", "budget"), 7);
        var found = await _uc.SearchAsync("nordea", null);
        found.Questions.Should().ContainSingle().Which.Question.Should().Be("Virker Budget med Nordea? Ring [skjult]");
        found.Topics.Should().Equal(("Budget", 1));
        (await _uc.SearchAsync("nordea", null, QuestionRules.Bogshoppen)).Total.Should().Be(0);

        await _uc.UpdateAsync(q.Id, new("Virker Budget med Nordea?", "Ja, med CSV.", "Budget"));
        (await _uc.SearchAsync("csv", null)).Questions.Should().ContainSingle();

        await _uc.RemoveAsync(q.Id);
        (await _uc.SearchAsync(null, null)).Total.Should().Be(0);
    }
}

public class TrialRequestTests
{
    private sealed class FakeKartotek : ICustomerGateway
    {
        public List<CustomerSignUp> SignedUp = [];
        public List<string> Unsubscribed = [];
        public Task<string?> SignUpAsync(CustomerSignUp s, CancellationToken ct = default) { SignedUp.Add(s); return Task.FromResult<string?>(null); }
        public Task UnsubscribeAsync(string email, CancellationToken ct = default) { Unsubscribed.Add(email); return Task.CompletedTask; }
        public Task<IReadOnlyList<string>> FreeAppsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ElPriser", "Temperatur", "Stem"]);
    }
    private sealed class FakeInbox : IOwnerInbox
    {
        public List<string> Texts = [];
        public Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default) { Texts.Add(text); return Task.CompletedTask; }
    }

    [Test]
    public async Task Trial_needs_the_ticked_consent_and_a_free_app_and_then_lands_in_kartotek_and_inbox()
    {
        var k = new FakeKartotek(); var inbox = new FakeInbox(); var uc = new Customers(k, inbox);
        (await uc.RequestTrialAsync("ElPriser", "Ole", "Ole@Gmail.com", consent: false)).Should().StartWith("Sæt flueben");
        (await uc.RequestTrialAsync("Tilbud", "Ole", "Ole@Gmail.com", consent: true)).Should().Be(TrialRules.NotFree);   // not on the free list
        k.SignedUp.Should().BeEmpty();
        (await uc.RequestTrialAsync("elpriser", "Ole", " Ole@Gmail.com ", consent: true)).Should().BeNull();
        k.SignedUp.Should().ContainSingle().Which.Should().Be(new CustomerSignUp("ole@gmail.com", "Ole", CustomerOf.ITMartin, "elpriser",
            TrialRules.Source("elpriser"), TrialRules.ConsentText, "elpriser"));
        TrialRules.ConsentText.Should().Contain("alle de gratis apps").And.Contain("Ingen reklamer, ingen nyhedsbreve").And.Contain("afmelde");
        inbox.Texts.Should().ContainSingle().Which.Should().Contain("elpriser").And.Contain("ole@gmail.com");
    }

    [Test]
    public async Task Unsubscribe_takes_just_the_email()
    {
        var k = new FakeKartotek(); var uc = new Customers(k, new FakeInbox());
        (await uc.UnsubscribeAsync("")).Should().NotBeNull();
        (await uc.UnsubscribeAsync(" OLE@gmail.com")).Should().BeNull();
        k.Unsubscribed.Should().Equal("ole@gmail.com");
    }
}

public class BogshoppenFollowTests
{
    private sealed class Kartotek : ICustomerGateway
    {
        public List<(string Email, CustomerOf Company, string Source, string Interests)> Rows = [];
        public Task<string?> SignUpAsync(CustomerSignUp s, CancellationToken ct = default)
        { Rows.Add((s.Email, s.Company, s.Source, s.Interests)); return Task.FromResult<string?>(null); }
        public Task UnsubscribeAsync(string email, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> FreeAppsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
    private sealed class NoInbox : IOwnerInbox { public Task NewRequestAsync(string n, string e, string t, CancellationToken ct = default) => Task.CompletedTask; }

    [Test]
    public async Task Follow_keeps_only_known_titles_plus_the_note_and_needs_consent()
    {
        var k = new Kartotek(); var uc = new Customers(k, new NoInbox());
        (await uc.FollowBogshoppenAsync("Ole", "ole@gmail.com", ["Tintin"], null, consent: false)).Should().NotBeNull();
        (await uc.FollowBogshoppenAsync("Ole", "Ole@Gmail.com", ["Tintin", "Hacket felt", "Asterix"], "Tintin nr. 5", consent: true)).Should().BeNull();
        k.Rows.Should().ContainSingle().Which.Should().Be(("ole@gmail.com", CustomerOf.Bogshoppen, BogshoppenRules.Source, "Tintin, Asterix | Tintin nr. 5"));
        BogshoppenRules.ConsentText.Should().Contain("Ingen reklamer, ingen nyhedsbreve").And.Contain("afmelde");
    }
}
