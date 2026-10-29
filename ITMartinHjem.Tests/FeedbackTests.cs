using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinHjem.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Tests;

// Kritik (2026-10-29): ros, ris og forslag - nothing is shown without the writer's yes AND Martin's Vis.
public class FeedbackTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RecordingInbox : IOwnerInbox
    {
        public List<string> Texts = [];
        public Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default) { Texts.Add(name + ": " + text); return Task.CompletedTask; }
    }

    private sealed class TestDbFactory(DbContextOptions<HjemQuestionsDb> o) : IDbContextFactory<HjemQuestionsDb>
    {
        public HjemQuestionsDb CreateDbContext() => new(o);
    }

    [Test]
    public void Nothing_is_shown_without_the_writers_yes()
    {
        var no = Feedback.Write("ris", "Du svarede for sent.", "", mayShow: false, Now);
        var act = () => no.Show("Undskyld");
        act.Should().Throw<InvalidOperationException>();
        FeedbackRules.Public([no]).Should().BeEmpty();

        var yes = Feedback.Write("ros", "Super hjælp med billederne!", "Jytte", mayShow: true, Now);
        FeedbackRules.Public([yes]).Should().BeEmpty();   // not before Martin presses Vis
        yes.Show("Tak!");
        FeedbackRules.Public([yes, no]).Should().ContainSingle().Which.Reply.Should().Be("Tak!");
    }

    [Test]
    public void It_waits_for_Martin_until_he_answers_or_shows_it()
    {
        var a = Feedback.Write("ris", "Du svarede for sent.", "", mayShow: false, Now);
        var b = Feedback.Write("ros", "Godt!", "", mayShow: true, Now);
        (a.Waiting, b.Waiting).Should().Be((true, true));
        a.Answer("Undskyld – det gør jeg bedre.");
        b.Show(null);
        (a.Waiting, b.Waiting).Should().Be((false, false));
    }

    [Test]
    public void Phone_and_email_are_taken_out_and_kind_falls_back_to_ros()
    {
        var f = Feedback.Write("noget", "Ring til mig på 31 19 47 30 eller skriv til a@b.dk", "", true, Now);
        f.Text.Should().NotContain("31 19 47 30").And.NotContain("a@b.dk");
        f.Kind.Should().Be(FeedbackRules.Praise);
        var empty = () => Feedback.Write("ris", "   ", "", true, Now);
        empty.Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task Written_feedback_is_saved_and_Martin_gets_it_in_the_chat()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<HjemQuestionsDb>().UseSqlite(conn).Options;
        await using (var db = new HjemQuestionsDb(opts)) await HjemQuestionsDb.EnsureTablesAsync(db);
        var inbox = new RecordingInbox();
        var uc = new FeedbackUseCases(new FeedbackStore(new TestDbFactory(opts)), inbox, TimeProvider.System);

        var f = await uc.WriteAsync("forslag", "Lav en videoaften!", "", true);
        inbox.Texts.Should().ContainSingle().Which.Should().Contain("Anonym").And.Contain("forslag").And.Contain("Lav en videoaften!");
        (await uc.PublicAsync()).Should().BeEmpty();
        (await uc.ShowAsync(f.Id, "God idé")).Should().BeTrue();
        (await uc.PublicAsync()).Should().ContainSingle().Which.Reply.Should().Be("God idé");
        await uc.RemoveAsync(f.Id);
        (await uc.AllAsync()).Should().BeEmpty();
    }
}
