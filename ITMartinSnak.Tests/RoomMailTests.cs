using FluentAssertions;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Tests;

// 2026-11-10: one "Martin har skrevet" mail to a customer without beskeder - never nagging.
public class RoomMailTests
{
    private static readonly DateTime T = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static Room R(string mail = "ole@gmail.com", DateTime? mailedAt = null) =>
        RoomRules.New("Ole", "kunde-1", T.AddDays(-1)) with { Mail = mail, MailedAt = mailedAt };
    private static Message Host(long id, DateTime at) => new(id, "Martin", "Hej Ole", at, Host: true);

    [Test]
    public void Mails_ten_minutes_after_Martin_wrote_when_unread_and_no_phone()
    {
        var last = Host(3, T);
        RoomMailRules.ShouldNudge(R(), last, [], 0, "Martin", T.AddMinutes(9)).Should().BeFalse();    // give them a moment
        RoomMailRules.ShouldNudge(R(), last, [], 0, "Martin", T.AddMinutes(10)).Should().BeTrue();
        RoomMailRules.ShouldNudge(R(), last, [], 1, "Martin", T.AddMinutes(30)).Should().BeFalse();   // gets beskeder
        RoomMailRules.ShouldNudge(R(""), last, [], 0, "Martin", T.AddMinutes(30)).Should().BeFalse();  // no address
        RoomMailRules.ShouldNudge(R(), last with { Host = false }, [], 0, "Martin", T.AddMinutes(30)).Should().BeFalse();
        var read = new Seen("Ole", 3, T.AddMinutes(2));
        RoomMailRules.ShouldNudge(R(), last, [read], 0, "Martin", T.AddMinutes(30)).Should().BeFalse();
        var martinRead = new Seen("Martin", 3, T);
        RoomMailRules.ShouldNudge(R(), last, [martinRead], 0, "Martin", T.AddMinutes(30)).Should().BeTrue();
    }

    [Test]
    public void After_one_mail_the_next_only_comes_when_the_customer_has_been_in_again()
    {
        var mailed = T.AddMinutes(10);
        var newer = Host(5, T.AddHours(1));
        RoomMailRules.ShouldNudge(R(mailedAt: mailed), newer, [], 0, "Martin", T.AddHours(2)).Should().BeFalse();
        var visited = new Seen("Ole", 3, T.AddMinutes(40));
        RoomMailRules.ShouldNudge(R(mailedAt: mailed), newer, [visited], 0, "Martin", T.AddHours(2)).Should().BeTrue();
        RoomMailRules.ShouldNudge(R(mailedAt: mailed), Host(3, T), [visited], 0, "Martin", T.AddHours(2)).Should().BeFalse();   // same old message
    }

    [Test]
    public void Mail_addresses_are_cleaned_and_the_mail_holds_only_the_link()
    {
        RoomMailRules.CleanMail("  Ole@Gmail.COM ").Should().Be("ole@gmail.com");
        RoomMailRules.CleanMail("ikke en mail").Should().BeEmpty();
        var (subject, body) = RoomMailRules.Nudge("Ole Hansen", "https://snak.itmartin.dk/r/abc");
        subject.Should().Be("Martin har skrevet til dig");
        body.Should().StartWith("Hej Ole\n").And.Contain("https://snak.itmartin.dk/r/abc").And.NotContain("Hej Ole\n\nHej Ole");
    }
}
