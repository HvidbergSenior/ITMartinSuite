using FluentAssertions;
using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Tests;

public class MailRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> Nobody = new();

    private static MailHeader Mail(string from, string subject = "Hej", bool list = false, string? unsub = null, bool auto = false,
        GmailTab tab = GmailTab.None, bool toMe = true, bool answered = false, int daysAgo = 1, string id = "m1") =>
        new(1, id, from, "", subject, Now.AddDays(-daysAgo), false, answered, toMe, unsub, false, list, auto, tab);

    [Test]
    public void Eboks_notice_is_digital_post_even_from_noreply()
    {
        MailRules.Categorize(Mail("noreply@e-boks.dk", "Du har fået ny post", auto: true), Nobody).Should().Be(MailCategory.DigitalPost);
        MailRules.Categorize(Mail("ikke-svar@mit.dk", "Ny post fra Aarhus Kommune"), Nobody).Should().Be(MailCategory.DigitalPost);
    }

    [Test]
    public void Akasse_and_jobnet_are_urgent_unless_newsletter()
    {
        MailRules.Categorize(Mail("post@ase.dk", "Referat af samtale"), Nobody).Should().Be(MailCategory.Haster);
        MailRules.Categorize(Mail("noreply@jobnet.dk", "Husk din joblog"), Nobody).Should().Be(MailCategory.Haster);
        MailRules.Categorize(Mail("nyt@ase.dk", "Nyhedsbrev november", list: true, unsub: "<mailto:x>"), Nobody).Should().Be(MailCategory.Nyhedsbreve);
    }

    [Test]
    public void Deadline_word_from_unknown_sender_is_urgent()
        => MailRules.Categorize(Mail("kontor@firma.dk", "Rykker: faktura forfalden"), Nobody).Should().Be(MailCategory.Haster);

    [Test]
    public void Person_i_wrote_to_needs_reply_until_answered()
    {
        var wrote = new HashSet<string> { "rico@firma.dk" };
        var m = Mail("rico@firma.dk", "Kasser til lageret?");
        MailRules.Categorize(m, wrote).Should().Be(MailCategory.SkalSvares);
        MailRules.WaitsForReply(m, MailCategory.SkalSvares, Now).Should().BeTrue();
        MailRules.WaitsForReply(m with { Answered = true }, MailCategory.SkalSvares, Now).Should().BeFalse();
    }

    [Test]
    public void My_own_mail_never_waits_for_reply()
        => MailRules.Categorize(Mail("itmartin@mensa.dk", "Re: tilbud"), Nobody, new HashSet<string> { "itmartin@mensa.dk" }).Should().Be(MailCategory.Andet);

    [Test]
    public void Machine_security_notice_is_not_urgent()
        => MailRules.Categorize(Mail("no-reply@accounts.google.com", "Sikkerhedsunderretning", auto: true), Nobody).Should().NotBe(MailCategory.Haster);

    [Test]
    public void Aka_is_urgent()
        => MailRules.Categorize(Mail("kontakt@aka.dk", "Underretning", auto: true, tab: GmailTab.Updates), Nobody).Should().Be(MailCategory.Haster);

    [Test]
    public void Private_mailbox_writing_to_me_is_a_person()
        => MailRules.Categorize(Mail("jytte@hotmail.com", "Spørgsmål om CD"), Nobody).Should().Be(MailCategory.SkalSvares);

    [Test]
    public void Bulk_sorted_by_gmail_tab_and_list()
    {
        MailRules.Categorize(Mail("tilbud@shop.dk", "50 % i dag", unsub: "<https://x>", tab: GmailTab.Promotions), Nobody).Should().Be(MailCategory.Reklame);
        MailRules.Categorize(Mail("news@blog.dk", "Ugens artikler", list: true), Nobody).Should().Be(MailCategory.Nyhedsbreve);
        MailRules.Categorize(Mail("notification@facebookmail.com", "Nogen kommenterede", auto: true, tab: GmailTab.Social), Nobody).Should().Be(MailCategory.Notifikationer);
        MailRules.Categorize(Mail("ordre@shop.dk", "Ordrebekræftelse #123"), Nobody).Should().Be(MailCategory.Kvitteringer);
    }

    [Test]
    public void Senders_are_grouped_and_counted()
    {
        var mails = Enumerable.Range(0, 5).Select(i => Mail("news@blog.dk", "Nr " + i, list: true, unsub: "<https://x>", id: "n" + i))
            .Append(Mail("jytte@hotmail.com", id: "j")).ToList();
        var o = MailRules.Build(mails, Nobody, 12, Now);
        o.Senders[0].Address.Should().Be("news@blog.dk");
        o.Senders[0].Count.Should().Be(5);
        o.Senders[0].CanUnsubscribe.Should().BeTrue();
        o.NeedsReply.Should().ContainSingle();
    }
}

public class FollowUpTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private static (MailHeader, MailCategory) Post(string id, int daysAgo) =>
        (new MailHeader(1, id, "noreply@e-boks.dk", "", "Ny post", new DateTimeOffset(Today.AddDays(-daysAgo).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            false, false, true, null, false, false, true, GmailTab.None), MailCategory.DigitalPost);

    [Test]
    public void Overdue_first_then_nearest_deadline_then_unopened()
    {
        var mails = new[] { Post("ny", 2), Post("frist10", 5), Post("over", 20), Post("frist3", 3) };
        var f = new Dictionary<string, FollowUp>
        {
            ["frist10"] = FollowUp.New("frist10").Read(Today.AddDays(10), null),
            ["over"] = FollowUp.New("over").Read(Today.AddDays(-2), "Jobmesse"),
            ["frist3"] = FollowUp.New("frist3").Read(Today.AddDays(3), null),
        };
        UrgentRules.Now(mails, f, Today).Select(i => i.Mail.MessageId).Should().Equal("over", "frist3", "frist10", "ny");
    }

    [Test]
    public void Klaret_disappears_and_old_untouched_notices_do_not_flood()
    {
        var mails = new[] { Post("klaret", 1), Post("gammel", 200), Post("gammelLaest", 200) };
        var f = new Dictionary<string, FollowUp>
        {
            ["klaret"] = FollowUp.New("klaret").Done(),
            ["gammelLaest"] = FollowUp.New("gammelLaest").Read(null, null),
        };
        UrgentRules.Now(mails, f, Today).Select(i => i.Mail.MessageId).Should().Equal("gammelLaest");
    }

    [Test]
    public void Baseline_hides_older_mails_but_keeps_my_deadlines()
    {
        var mails = new[] { Post("gammel", 10), Post("gammelFrist", 10), Post("ny", 1) };
        var f = new Dictionary<string, FollowUp> { ["gammelFrist"] = FollowUp.New("gammelFrist").Read(Today.AddDays(5), null) };
        var line = new DateTimeOffset(Today.AddDays(-3).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        UrgentRules.Now(mails, f, Today, baseline: line).Select(i => i.Mail.MessageId).Should().Equal("gammelFrist", "ny");
    }

    [Test]
    public void Note_is_short()
        => FollowUp.New("x").Read(null, new string('a', 200)).Note.Length.Should().Be(FollowUp.MaxNote);
}
