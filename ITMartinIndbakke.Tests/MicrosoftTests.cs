using System.Text;
using FluentAssertions;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinIndbakke.Tests;

// "Log ind med Microsoft" for Outlook.com / Hotmail (2026-11-05).
public class MicrosoftTests
{
    private static (MailSessions S, Mock<IMicrosoftLogin> Ms) Make(bool customerOk = true)
    {
        var box = new Mock<IMailbox>();
        box.Setup(b => b.ScanAsync(It.IsAny<int>(), It.IsAny<IProgress<(int, int)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxScan([], new HashSet<string>(), new HashSet<string>()));
        var ms = new Mock<IMicrosoftLogin>();
        ms.Setup(m => m.IsConfigured).Returns(true);
        ms.Setup(m => m.AuthorizeUrl(It.IsAny<string>(), It.IsAny<string>())).Returns((string st, string ch) => $"https://login.example/?state={st}&c={ch}");
        ms.Setup(m => m.FinishAsync("good-code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("lukas@hotmail.com", new MailAccess(box.Object, Mock.Of<IMailboxCleaner>(), Mock.Of<IUnsubscriber>()), (string?)null));
        var stores = new Mock<IAccountStores>();
        stores.Setup(s => s.FollowUps(It.IsAny<string>())).Returns(Mock.Of<IFollowUpStore>(f => f.All() == new Dictionary<string, FollowUp>()));
        stores.Setup(s => s.Log(It.IsAny<string>())).Returns(Mock.Of<ICleanupLog>());
        var customers = new Mock<ICustomerCheck>();
        customers.Setup(c => c.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerOk ? (true, "Lukas", "") : (false, "", "Min Indbakke er ikke slået til for dit nummer."));
        return (new MailSessions(Mock.Of<IMailAccessFactory>(), stores.Object, customers.Object, NullLoggerFactory.Instance, TimeProvider.System, ms.Object), ms);
    }

    private static string StateOf(string url) => url.Split("state=")[1].Split('&')[0];

    [Test]
    public async Task The_number_is_checked_before_anyone_goes_to_Microsoft()
    {
        var (s, ms) = Make(customerOk: false);
        var (url, error) = await s.BeginMicrosoftAsync("5132 5692");
        url.Should().BeNull();
        error.Should().Contain("ikke slået til");
        ms.Verify(m => m.AuthorizeUrl(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Back_from_Microsoft_gives_the_persons_own_workspace_and_the_code_works_only_once()
    {
        var (s, _) = Make();
        var (url, _) = await s.BeginMicrosoftAsync("51325692");
        var state = StateOf(url!);
        var r = await s.FinishMicrosoftAsync(state, "good-code");
        r.Error.Should().BeNull();
        s.Get(r.Token)!.Name.Should().Be("Lukas");
        (await s.FinishMicrosoftAsync(state, "good-code")).Token.Should().BeNull();   // a state is used once
    }

    [Test]
    public async Task Cancel_at_Microsoft_or_an_unknown_state_gives_a_Danish_message()
    {
        var (s, _) = Make();
        var (url, _) = await s.BeginMicrosoftAsync("51325692");
        (await s.FinishMicrosoftAsync(StateOf(url!), null)).Error.Should().Contain("Annuller");
        (await s.FinishMicrosoftAsync("not-ours", "good-code")).Error.Should().Contain("Prøv igen");
    }

    [Test]
    public void The_mail_address_is_read_from_Microsofts_id_token()
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = "x." + Part("""{"preferred_username":"Lukas@Hotmail.com"}""") + ".y";
        ITMartinIndbakke.Infrastructure.MicrosoftLogin.EmailFrom(token).Should().Be("lukas@hotmail.com");
        ITMartinIndbakke.Infrastructure.MicrosoftLogin.EmailFrom("garbage").Should().BeNull();
    }
}
