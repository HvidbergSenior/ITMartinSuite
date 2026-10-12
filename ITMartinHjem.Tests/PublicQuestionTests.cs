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

        await _uc.UpdateAsync(q.Id, new("Virker Budget med Nordea?", "Ja, med CSV.", "Budget"));
        (await _uc.SearchAsync("csv", null)).Questions.Should().ContainSingle();

        await _uc.RemoveAsync(q.Id);
        (await _uc.SearchAsync(null, null)).Total.Should().Be(0);
    }
}
