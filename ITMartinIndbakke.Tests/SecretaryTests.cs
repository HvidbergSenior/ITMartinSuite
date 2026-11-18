using FluentAssertions;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using NUnit.Framework;

namespace ITMartinIndbakke.Tests;

/// <summary>The automatic secretary (2026-11-18): dates in mail text + "Du venter på svar".</summary>
public class SecretaryTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Test]
    public void Finds_a_meeting_with_time()
    {
        var hits = DateFinder.Find("Vi har to samtaler, og den første ligger fredag d. 20. november kl. 10.00. Vi kan mødes i Ry.", Today);
        hits.Should().ContainSingle();
        hits[0].Date.Should().Be(new DateOnly(2026, 11, 20));
        hits[0].Time.Should().Be(new TimeOnly(10, 0));
        hits[0].Word.Should().Be("samtale");
    }

    [Test]
    public void Finds_numeric_date_and_deadline_word()
    {
        var hits = DateFinder.Find("Svar senest 4.12 – ellers lukkes sagen.", Today);
        hits.Should().ContainSingle();
        hits[0].Date.Should().Be(new DateOnly(2026, 12, 4));
        hits[0].Word.Should().Be("senest");
    }

    [Test]
    public void Booked_meeting_from_the_a_kasse()
    {
        var hits = DateFinder.Find("Vi har booket dig til en samtale om din rådighed den 26. november 2026 kl. 09:00.", Today);
        hits.Should().ContainSingle();
        hits[0].Date.Should().Be(new DateOnly(2026, 11, 26));
        hits[0].Time.Should().Be(new TimeOnly(9, 0));
    }

    [TestCase("Ordre 12.11 er sendt med GLS.")]        // no keyword near it
    [TestCase("Mødet var den 3.11, tak for i dag.")]   // in the past
    [TestCase("Version 1.2.3 er klar.")]               // not a date
    [TestCase("Kurset starter 9.11 kl. 8.")]           // passed this year - not next year
    [TestCase("Mødet i august, den 3.8.")]
    public void Ignores_dates_without_a_reason(string text) =>
        DateFinder.Find(text, Today).Should().BeEmpty();

    [Test]
    public void Next_year_when_the_day_has_passed()
    {
        var hits = DateFinder.Find("Holdet starter 2.2 kl. 19.15.", Today);
        hits.Should().ContainSingle();
        hits[0].Date.Should().Be(new DateOnly(2027, 2, 2));
        hits[0].Time.Should().Be(new TimeOnly(19, 15));
    }

    [Test]
    public void Title_is_short_and_clear()
    {
        var d = new MailDate("x", "m", "Michael", "Accelerator-sparring", new DateOnly(2026, 11, 20), new TimeOnly(10, 0), "samtale",
            DateStatus.Ny, DateTimeOffset.UtcNow);
        d.Title().Should().Be("Michael: Samtale 20.11 kl. 10.00");
        d.HuskKey.Should().StartWith("mail-dato-");
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> Mine = ["me@gmail.com"];

    private static MailHeader In(ulong thread, string from, DateTimeOffset at, string subject = "Re: x") =>
        new(1, Guid.NewGuid().ToString(), from, "", subject, at, true, false, true, null, false, false, false, GmailTab.None, ThreadId: thread);

    [Test]
    public void I_wrote_last_and_nobody_answered()
    {
        var sent = new[] { new SentMail(7, "anna@lof.dk", "Anna", "Hold i foråret", Now.AddDays(-3), 1) };
        var found = AwaitingRules.Find(sent, [In(7, "anna@lof.dk", Now.AddDays(-4))], Mine, Now);
        found.Should().ContainSingle();
        found[0].Days(Now).Should().Be(3);
        found[0].HuskKey.Should().Be("mail-venter-7");
    }

    [Test]
    public void An_answer_ends_it_but_an_auto_reply_does_not()
    {
        var sent = new[] { new SentMail(7, "anna@lof.dk", "Anna", "Hold", Now.AddDays(-3), 1) };
        AwaitingRules.Find(sent, [In(7, "anna@lof.dk", Now.AddDays(-1))], Mine, Now).Should().BeEmpty();
        AwaitingRules.Find(sent, [In(7, "anna@lof.dk", Now.AddDays(-1), "Autosvar: Hold")], Mine, Now).Should().ContainSingle();
    }

    [TestCase("Dit kundenummer hos ITMartin og Bogshoppen", "kunde@gmail.com", 1)]
    [TestCase("Hold", "noreply@firma.dk", 1)]
    [TestCase("Hold", "a@b.dk", 12)]   // a mail to many people
    public void Not_waiting_for_automatic_or_bulk(string subject, string to, int recipients) =>
        AwaitingRules.Find([new SentMail(9, to, "", subject, Now.AddDays(-5), recipients)], [], Mine, Now).Should().BeEmpty();

    [Test]
    public void Answer_since_finds_the_reply_to_a_watched_conversation()
    {
        var inbox = new[] { In(7, "anna@lof.dk", Now.AddDays(-5)), In(7, "anna@lof.dk", Now.AddHours(-2)) };
        AwaitingRules.AnswerSince(7, Now.AddDays(-3), inbox, Mine).Should().NotBeNull();
        AwaitingRules.AnswerSince(7, Now.AddHours(-1), inbox, Mine).Should().BeNull();
    }

    [Test]
    public void Candidates_skip_lists_myself_and_old_mail()
    {
        var person = In(1, "anna@lof.dk", Now.AddDays(-2));
        var list = person with { MessageId = "list", IsMailingList = true };
        var me = person with { MessageId = "me", FromAddress = "me@gmail.com" };
        var old = person with { MessageId = "old", Date = Now.AddDays(-40) };
        var scan = new MailboxScan([person, list, me, old], new HashSet<string>(), Mine);
        Secretary.Candidates(scan, _ => false, Now).Select(m => m.MessageId).Should().Equal(person.MessageId);
    }
}
