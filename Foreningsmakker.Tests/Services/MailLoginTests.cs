using Foreningsmakker.Application;
using Foreningsmakker.Domain;
using Foreningsmakker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Foreningsmakker.Tests.Services;

// "Log ind med mail" (2026-11-18): a one-time link, 30 minutes, and the same answer whether the mail is known or not.
public class MailLoginTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Link_works_once()
    {
        var m = new Member();
        var token = m.StartMailLogin(Now);
        m.UseMailLogin(token, Now.AddMinutes(5)).Should().BeTrue();
        m.UseMailLogin(token, Now.AddMinutes(6)).Should().BeFalse();
    }

    [Test]
    public void Link_stops_after_30_minutes()
    {
        var m = new Member();
        var token = m.StartMailLogin(Now);
        m.UseMailLogin(token, Now.AddMinutes(31)).Should().BeFalse();
    }

    [Test]
    public void New_link_replaces_the_old_one()
    {
        var m = new Member();
        var old = m.StartMailLogin(Now);
        m.StartMailLogin(Now);
        m.UseMailLogin(old, Now).Should().BeFalse();
    }

    [Test]
    public async Task Unknown_mail_gets_the_same_answer_and_no_mail()
    {
        var (access, _, mail, group) = await CreateAsync();
        var answer = await access.SendLoginLinkAsync(group, "ukendt@mail.dk", "https://forening.test/");
        answer.Should().Be(TeamAccess.MailLoginSent);
        mail.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task A_fourth_link_within_15_minutes_is_refused_for_any_mail()
    {
        var (access, _, mail, group) = await CreateAsync();
        for (var i = 0; i < 3; i++) await access.SendLoginLinkAsync(group, "ole@mail.dk", "https://f.test/");
        (await access.SendLoginLinkAsync(group, "ole@mail.dk", "https://f.test/")).Should().Contain("vent 15 minutter");
        mail.Sent.Should().HaveCount(3);
        (await access.SendLoginLinkAsync(group, "ukendt@mail.dk", "https://f.test/")).Should().Be(TeamAccess.MailLoginSent);
    }

    [Test]
    public async Task Known_mail_gets_a_link_that_logs_in_once()
    {
        var (access, _, mail, group) = await CreateAsync();
        var answer = await access.SendLoginLinkAsync(group, " Ole@Mail.DK ", "https://forening.test/");
        answer.Should().Be(TeamAccess.MailLoginSent);
        mail.Sent.Should().ContainSingle().Which.To.Should().Be("ole@mail.dk");

        var token = mail.Sent[0].Body.Split("/k/")[1].Split('\n')[0];
        var session = await access.LoginByMailLinkAsync(token);
        session.Should().NotBeNull();
        session!.Member.Group.Slug.Should().Be("roklubben");
        (await access.LoginByMailLinkAsync(token)).Should().BeNull();
    }

    [Test]
    public async Task Admin_adds_a_person_and_the_invitation_goes_out()
    {
        var (_, people, mail, group) = await CreateAsync();
        var admin = new Member { Name = "Martin", Role = "Formand, Admin" };
        var (error, mailed) = await people.AddAndInviteAsync(group, "Kirsten", "kirsten@mail.dk", "Kasserer", admin, "https://forening.test/");
        error.Should().BeNull();
        mailed.Should().BeTrue();
        mail.Sent.Should().ContainSingle().Which.Body.Should().Contain("som Kasserer").And.Contain("/l/");

        var again = await people.AddAndInviteAsync(group, "Kirsten 2", "KIRSTEN@mail.dk", "", admin, "https://forening.test/");
        again.Error.Should().Contain("allerede");
    }

    [Test]
    public async Task The_first_person_becomes_admin_gets_logged_in_and_mailed_but_only_once()
    {
        var (access, _, mail, _) = await CreateAsync();
        var r = await access.FoundAsync("hammel", "Borgerforeningen", "Martin", "Martin@Mail.dk", "", "https://f.test/");
        r.Error.Should().BeNull();
        r.Session.Should().NotBeNull();
        r.AdminPin.Should().HaveLength(6);
        mail.Sent.Should().ContainSingle().Which.Body.Should().Contain("/l/").And.Contain(r.AdminPin!);
        var g = (await access.GroupAsync("hammel"))!;
        access.UnlockAdmin(g, r.AdminPin!).Should().Be(AdminUnlock.Ok);

        var again = await access.FoundAsync("hammel", "", "Fremmed", "x@mail.dk", "", "https://f.test/");
        again.Error.Should().Contain("allerede medlemmer");
    }

    private static async Task<(TeamAccess, TeamMembers, FakeMail, Group)> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        var options = new DbContextOptionsBuilder<ClubDb>().UseSqlite(connection).Options;
        new ClubDb(options).Database.EnsureCreated();
        var teams = new TeamStore(new Factory(options));
        var group = new Group { Slug = "roklubben", Name = "Roklubben", InviteCode = "x", AdminPin = "x" };
        await teams.AddGroupAsync(group);
        await teams.AddMemberAsync(new Member { GroupId = group.Id, Name = "Ole", Email = "ole@mail.dk" });
        var mail = new FakeMail();
        var access = new TeamAccess(teams, new AdminPinLimiter(), new MemberPinLimiter(), new ClubOptions(), new FixedClock(Now), mail, new MailLinkLimiter(new FixedClock(Now)));
        return (access, new TeamMembers(teams, mail), mail, group);
    }

    private sealed class Factory(DbContextOptions<ClubDb> options) : IDbContextFactory<ClubDb>
    {
        public ClubDb CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class FakeMail : IMailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public bool Configured => true;
        public Task<bool> SendAsync(string to, string fromName, string subject, string body)
        {
            Sent.Add((to, subject, body));
            return Task.FromResult(true);
        }
    }
}
