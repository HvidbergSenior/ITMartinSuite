using FluentAssertions;
using ITMartinIndbakke.Domain;

namespace ITMartinIndbakke.Tests;

public class CleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static MailHeader Mail(string id, string from = "info@mail.lidlplus.dk", string? unsub = "<https://lidl.dk/u?x=1>", bool oneClick = true, int daysAgo = 1) =>
        new((uint)id.GetHashCode(), id, from, "Lidl Plus", "Tilbud " + id, Now.AddDays(-daysAgo), true, false, true, unsub, oneClick, true, false, GmailTab.Promotions);

    [Test]
    public void Only_junk_can_be_deleted_and_never_starred()
    {
        var m = Mail("a");
        CleanupRules.CanDelete(m, MailCategory.Reklame).Should().BeTrue();
        CleanupRules.CanDelete(m, MailCategory.Nyhedsbreve).Should().BeTrue();
        CleanupRules.CanDelete(m, MailCategory.Notifikationer).Should().BeTrue();
        CleanupRules.CanDelete(m with { Flagged = true }, MailCategory.Reklame).Should().BeFalse();
        foreach (var keep in new[] { MailCategory.DigitalPost, MailCategory.Haster, MailCategory.SkalSvares, MailCategory.Kvitteringer, MailCategory.Andet })
            CleanupRules.CanDelete(m, keep).Should().BeFalse(keep.ToString());
    }

    [Test]
    public void Preview_counts_keeps_receipts_and_says_why()
    {
        var all = new List<(MailHeader, MailCategory)>
        {
            (Mail("1", daysAgo: 1), MailCategory.Reklame),
            (Mail("2", daysAgo: 40), MailCategory.Reklame),
            (Mail("3"), MailCategory.Kvitteringer),
            (Mail("4", from: "other@x.dk"), MailCategory.Reklame),
        };
        var p = CleanupRules.Preview("INFO@mail.lidlplus.dk", all);
        p.ToDelete.Select(m => m.MessageId).Should().BeEquivalentTo(["1", "2"]);
        p.Kept.Should().ContainSingle(k => k.Count == 1 && k.Why.Contains("bogføring"));
        p.Oldest.Should().Be(Now.AddDays(-40));
        p.Unsubscribe!.Kind.Should().Be(UnsubscribeKind.OneClick);
    }

    [Test]
    public void Unsubscribe_header_is_parsed()
    {
        var t = UnsubscribeTarget.Parse("<mailto:unsub@list.dk?subject=Afmeld%20mig>, <https://list.dk/u/123>", oneClick: false);
        t.Https!.Host.Should().Be("list.dk");
        t.MailTo.Should().Be("unsub@list.dk");
        t.MailSubject.Should().Be("Afmeld mig");
        t.Kind.Should().Be(UnsubscribeKind.Mail);
        UnsubscribeTarget.Parse("<https://list.dk/u/123>", oneClick: false).Kind.Should().Be(UnsubscribeKind.Page);
        UnsubscribeTarget.Parse("<http://insecure.dk/u>", oneClick: true).IsUsable.Should().BeFalse();
    }

    [Test]
    public void Undo_only_inside_papirkurv_time()
    {
        var a = new CleanupAction(Guid.NewGuid(), Now, "x", "x", ["1"], "", false);
        a.CanUndo(Now.AddDays(10)).Should().BeTrue();
        a.CanUndo(Now.AddDays(30)).Should().BeFalse();
        (a with { Restored = true }).CanUndo(Now).Should().BeFalse();
    }
}

public class OldCleanupTests
{
    [Test]
    public void Queries_keep_starred_and_authorities()
    {
        var d = new DateOnly(2026, 1, 1);
        OldCleanupRules.TrashQuery(d, OldMode.SletAlt).Should().Be("before:2026/01/01 -is:starred");
        var q = OldCleanupRules.TrashQuery(d, OldMode.SletReklame);
        q.Should().StartWith("before:2026/01/01 (category:promotions OR category:social) -is:starred -from:(");
        q.Should().Contain("aka.dk").And.Contain("e-boks.dk").And.Contain("skat.dk");
    }

    [Test]
    public void Date_must_be_sensible_and_undo_works_by_label()
    {
        var today = new DateOnly(2026, 11, 4);
        OldCleanupRules.IsValid(new DateOnly(2026, 1, 1), today).Should().BeTrue();
        OldCleanupRules.IsValid(new DateOnly(2027, 1, 1), today).Should().BeFalse();
        var a = new CleanupAction(Guid.NewGuid(), DateTimeOffset.UtcNow, "", "Alt før", [], "", false, "Kolibri-oprydning-x", 10, 5);
        a.CanUndo(DateTimeOffset.UtcNow).Should().BeTrue();
    }
}
