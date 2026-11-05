using FluentAssertions;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinIndbakke.Tests;

// Min Indbakke (2026-11-05): number + Gmail + key every login; the key lives in memory only and is gone after 30 quiet minutes.
public class SessionTests
{
    private const string Key = "abcdefghijklmnop";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (MailSessions Sessions, Clock Clock, Mock<IMailAccessFactory> Mail) Make(bool customerOk = true, string? gmailSays = null)
    {
        var clock = new Clock();
        var mailbox = new Mock<IMailbox>();
        mailbox.Setup(m => m.IsConfigured).Returns(true);
        mailbox.Setup(m => m.ScanAsync(It.IsAny<int>(), It.IsAny<IProgress<(int, int)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxScan([], new HashSet<string>(), new HashSet<string>()));
        var mail = new Mock<IMailAccessFactory>();
        mail.Setup(m => m.CheckLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(gmailSays);
        mail.Setup(m => m.Open(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new MailAccess(mailbox.Object, Mock.Of<IMailboxCleaner>(), Mock.Of<IUnsubscriber>()));
        var stores = new Mock<IAccountStores>();
        stores.Setup(s => s.FollowUps(It.IsAny<string>())).Returns(Mock.Of<IFollowUpStore>(f => f.All() == new Dictionary<string, FollowUp>()));
        stores.Setup(s => s.Log(It.IsAny<string>())).Returns(Mock.Of<ICleanupLog>());
        var customers = new Mock<ICustomerCheck>();
        customers.Setup(c => c.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerOk ? (true, "Anna", "") : (false, "", "Min Indbakke er ikke slået til for dit nummer."));
        return (new MailSessions(mail.Object, stores.Object, customers.Object, NullLoggerFactory.Instance, clock), clock, mail);
    }

    [Test]
    public async Task A_good_login_opens_the_persons_own_workspace()
    {
        var (s, _, mail) = Make();
        var r = await s.OpenAsync("1234 5678", "anna@gmail.com", "abcd efgh ijkl mnop");
        r.Error.Should().BeNull();
        s.Get(r.Token)!.Name.Should().Be("Anna");
        mail.Verify(m => m.Open("anna@gmail.com", Key), Times.Once);   // spaces taken out of the key
    }

    [TestCase("1234", "anna@gmail.com", Key, "8 cifre")]
    [TestCase("12345678", "anna", Key, "Gmail-adresse")]
    [TestCase("12345678", "anna@gmail.com", "kort", "16 bogstaver")]
    public async Task Wrong_fields_say_what_to_fix(string code, string email, string key, string says)
    {
        var (s, _, mail) = Make();
        var r = await s.OpenAsync(code, email, key);
        r.Token.Should().BeNull();
        r.Error.Should().Contain(says);
        mail.Verify(m => m.Open(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task A_number_Martin_has_not_opened_gets_no_access()
    {
        var (s, _, mail) = Make(customerOk: false);
        var r = await s.OpenAsync("12345678", "anna@gmail.com", Key);
        r.Token.Should().BeNull();
        r.Error.Should().Contain("ikke slået til");
        mail.Verify(m => m.CheckLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Gmail_saying_no_gives_no_session()
    {
        var (s, _, _) = Make(gmailSays: "Gmail sagde nej.");
        var r = await s.OpenAsync("12345678", "anna@gmail.com", Key);
        r.Token.Should().BeNull();
        r.Error.Should().Be("Gmail sagde nej.");
        s.OpenCount.Should().Be(0);
    }

    [Test]
    public async Task The_key_is_forgotten_after_30_quiet_minutes_but_use_keeps_it_alive()
    {
        var (s, clock, _) = Make();
        var token = (await s.OpenAsync("12345678", "anna@gmail.com", Key)).Token;
        clock.Now += TimeSpan.FromMinutes(25);
        s.Get(token).Should().NotBeNull();                         // used -> 30 more minutes
        clock.Now += TimeSpan.FromMinutes(25);
        s.Get(token).Should().NotBeNull();
        clock.Now += MinIndbakkeRules.IdleTimeout + TimeSpan.FromMinutes(1);
        s.Get(token).Should().BeNull();
        s.OpenCount.Should().Be(0);
    }

    [Test]
    public async Task Log_ud_drops_the_session_at_once()
    {
        var (s, _, _) = Make();
        var token = (await s.OpenAsync("12345678", "anna@gmail.com", Key)).Token;
        s.Close(token);
        s.Get(token).Should().BeNull();
    }

    [Test]
    public void The_account_folder_is_digits_only() =>
        MinIndbakkeRules.AccountKey("../12 34-56 78").Should().Be("kunde-12345678");
}
