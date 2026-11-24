using FluentAssertions;
using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class PasswordResetTests
{
    private sealed class FakeMail(bool on = true) : IMailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public bool Configured => on;
        public Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            Sent.Add((to, subject, body));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAccounts : IResetAccounts
    {
        public readonly Dictionary<string, string> Users = new() { ["anna@example.dk"] = "Anna" };
        public readonly List<(string Email, string Hash, DateTime Created, DateTime Expires, DateTime? Used)> Tickets = [];
        public string? NewPassword;

        public Task<string?> FindNameAsync(string email) => Task.FromResult(Users.TryGetValue(email, out var n) ? n : null);
        public Task<DateTime?> LastTicketUtcAsync(string email) =>
            Task.FromResult(Tickets.Where(t => t.Email == email).Select(t => (DateTime?)t.Created).Max());
        public Task SaveTicketAsync(string email, string hash, DateTime created, DateTime expires)
        {
            Tickets.Add((email, hash, created, expires, null));
            return Task.CompletedTask;
        }
        public Task<bool> TicketUsableAsync(string hash, DateTime now) =>
            Task.FromResult(Tickets.Any(t => t.Hash == hash && PasswordResetTicket.Usable(t.Expires, t.Used, now)));
        public Task<bool> UseTicketAsync(string hash, string pw, DateTime now)
        {
            var i = Tickets.FindIndex(t => t.Hash == hash && PasswordResetTicket.Usable(t.Expires, t.Used, now));
            if (i < 0) return Task.FromResult(false);
            Tickets[i] = Tickets[i] with { Used = now };
            NewPassword = pw;
            return Task.FromResult(true);
        }
    }

    private DateTime _now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private (PasswordResetUseCases Uc, FakeAccounts Acc, FakeMail Mail) Make(bool mailOn = true)
    {
        var acc = new FakeAccounts();
        var mail = new FakeMail(mailOn);
        return (new PasswordResetUseCases(acc, mail, () => _now), acc, mail);
    }

    private static string TokenFrom(string body) => body.Split("?t=")[1].Split('\n')[0].Trim();

    [Test]
    public async Task A_known_mail_gets_a_link_and_only_the_hash_is_stored()
    {
        var (uc, acc, mail) = Make();
        (await uc.RequestAsync(" Anna@Example.dk ", "https://minelpris.itmartin.dk/nulstil")).Should().Be(ResetRequest.Sent);
        mail.Sent.Should().ContainSingle(m => m.To == "anna@example.dk" && m.Body.Contains("Hej Anna"));
        var token = TokenFrom(mail.Sent[0].Body);
        acc.Tickets.Single().Hash.Should().Be(PasswordResetTicket.Hash(token)).And.NotBe(token);
    }

    [Test]
    public async Task An_unknown_mail_gets_the_same_answer_and_no_mail()
    {
        var (uc, _, mail) = Make();
        (await uc.RequestAsync("nobody@example.dk", "https://x/nulstil")).Should().Be(ResetRequest.Sent);
        mail.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task At_most_one_mail_every_two_minutes()
    {
        var (uc, _, mail) = Make();
        await uc.RequestAsync("anna@example.dk", "https://x/nulstil");
        _now = _now.AddSeconds(30);
        await uc.RequestAsync("anna@example.dk", "https://x/nulstil");
        mail.Sent.Should().HaveCount(1);
        _now = _now.AddMinutes(2);
        await uc.RequestAsync("anna@example.dk", "https://x/nulstil");
        mail.Sent.Should().HaveCount(2);
    }

    [Test]
    public async Task The_link_works_once_and_for_one_hour()
    {
        var (uc, acc, mail) = Make();
        await uc.RequestAsync("anna@example.dk", "https://x/nulstil");
        var token = TokenFrom(mail.Sent[0].Body);

        (await uc.CompleteAsync(token, "kort", "kort", 8)).Ok.Should().BeFalse();
        (await uc.CompleteAsync(token, "langtnyt1", "langtnyt2", 8)).Error.Should().Contain("ikke ens");
        (await uc.LinkWorksAsync(token)).Should().BeTrue();
        (await uc.CompleteAsync(token, "langtnyt1", "langtnyt1", 8)).Ok.Should().BeTrue();
        acc.NewPassword.Should().Be("langtnyt1");
        (await uc.CompleteAsync(token, "andetnyt1", "andetnyt1", 8)).Ok.Should().BeFalse();

        _now = _now.AddMinutes(3);
        await uc.RequestAsync("anna@example.dk", "https://x/nulstil");
        var second = TokenFrom(mail.Sent[1].Body);
        _now = _now.AddMinutes(61);
        (await uc.LinkWorksAsync(second)).Should().BeFalse();
        (await uc.CompleteAsync(second, "langtnyt1", "langtnyt1", 8)).Ok.Should().BeFalse();
    }

    [Test]
    public async Task Without_mail_settings_the_page_says_so()
    {
        var (uc, _, mail) = Make(mailOn: false);
        (await uc.RequestAsync("anna@example.dk", "https://x/nulstil")).Should().Be(ResetRequest.MailOff);
        mail.Sent.Should().BeEmpty();
    }

    [Test]
    public void Tokens_are_random_and_url_safe()
    {
        var a = PasswordResetTicket.Create(_now);
        var b = PasswordResetTicket.Create(_now);
        a.Token.Should().NotBe(b.Token).And.MatchRegex("^[A-Za-z0-9_-]{40,}$");
        a.ExpiresUtc.Should().Be(_now.AddHours(1));
    }
}
