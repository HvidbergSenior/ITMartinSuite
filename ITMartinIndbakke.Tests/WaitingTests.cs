using FluentAssertions;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinIndbakke.Tests;

// "Venter på dit svar" + "Afsluttet" (2026-11-05).
public class WaitingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static MailHeader Person(string id, bool answered = false, bool repliedLater = false) =>
        new(1, id, "anna@gmail.com", "Anna", "Hej Martin", Now.AddDays(-2), false, answered, true, null, false, false, false, GmailTab.None,
            RepliedLater: repliedLater);

    [Test]
    public void A_later_mail_from_me_in_the_conversation_counts_as_answered_even_without_Gmails_mark()
    {
        MailRules.WaitsForReply(Person("a"), MailCategory.SkalSvares, Now).Should().BeTrue();
        MailRules.WaitsForReply(Person("b", answered: true), MailCategory.SkalSvares, Now).Should().BeFalse();
        MailRules.WaitsForReply(Person("c", repliedLater: true), MailCategory.SkalSvares, Now).Should().BeFalse();
    }

    private sealed class MemStore : IFollowUpStore
    {
        private readonly Dictionary<string, FollowUp> _d = new();
        public IReadOnlyDictionary<string, FollowUp> All() => _d;
        public void Save(FollowUp f) => _d[f.MessageId] = f;
        public DateTimeOffset? Baseline { get; set; }
    }

    [Test]
    public async Task Afsluttet_takes_it_off_the_list_and_Fortryd_brings_it_back()
    {
        var box = new Mock<IMailbox>();
        box.Setup(b => b.ScanAsync(It.IsAny<int>(), It.IsAny<IProgress<(int, int)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxScan([Person("anna-1")], new HashSet<string> { "anna@gmail.com" }, new HashSet<string> { "me@gmail.com" }));
        var scanner = new MailScanner(box.Object, NullLogger<MailScanner>.Instance, TimeProvider.System);
        scanner.Start(12);
        for (var i = 0; i < 50 && scanner.Last is null; i++) await Task.Delay(20);
        var follow = new FollowUps(new MemStore(), scanner, TimeProvider.System);

        follow.Waiting().Should().ContainSingle(x => x.Mail.MessageId == "anna-1");
        follow.MarkDone("anna-1").Should().BeNull();
        follow.Waiting().Should().BeEmpty();
        follow.Closed().Should().ContainSingle();
        follow.Reopen("anna-1").Should().BeNull();
        follow.Waiting().Should().ContainSingle();
    }
}
