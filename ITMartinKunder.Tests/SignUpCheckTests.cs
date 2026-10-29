using FluentAssertions;
using ITMartinKunder.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinKunder.Tests;

// Sign-up checks (user 2026-10-29 "Should there be some validation when they signup?" -> 1 Mente du, 2 capitals,
// 3 Måske dobbelt, 4 mail server). The case that started it: rikkebunch@hotmaol.com, then @hotmail.com 18 min later.
public class SignUpCheckTests
{
    [TestCase("rikkebunch@hotmaol.com", "rikkebunch@hotmail.com")]
    [TestCase("ole@gmial.com", "ole@gmail.com")]
    [TestCase("ole@gmail.con", "ole@gmail.com")]
    [TestCase("ole@hotmial.dk", "ole@hotmail.dk")]
    [TestCase("ole@outlok.com", "ole@outlook.com")]
    [TestCase("ole@live.dkk", "ole@live.dk")]
    [TestCase("Ole@Yaho.dk", "ole@yahoo.dk")]
    public void A_typo_in_a_big_mail_provider_gets_a_suggestion(string typed, string meant) =>
        KundeWords.SuggestEmail(typed).Should().Be(meant);

    [TestCase("ole@gmail.com")]
    [TestCase("ole@hotmail.dk")]
    [TestCase("ole@ymail.com")]      // Yahoo's own - one letter from gmail
    [TestCase("ole@email.com")]
    [TestCase("ole@life.dk")]        // a real domain next to live.dk
    [TestCase("ole@okoniewski.dk")]
    [TestCase("ole@hotmail.co.uk")]
    [TestCase("ikke-en-mail")]
    public void A_right_or_unknown_address_gets_none(string typed) =>
        KundeWords.SuggestEmail(typed).Should().BeNull();

    [TestCase("mette vibe sørensen", "Mette Vibe Sørensen")]
    [TestCase("  christian   egelund ", "Christian Egelund")]
    [TestCase("anne-marie hansen", "Anne-Marie Hansen")]
    [TestCase("ole de neergaard", "Ole de Neergaard")]
    [TestCase("ITMartin", "ITMartin")]
    [TestCase("Riksi", "Riksi")]
    public void Names_get_capitals(string typed, string saved) =>
        CustomerNames.Nice(typed).Should().Be(saved);

    [Test]
    public void The_same_person_twice_is_flagged_both_ways()
    {
        var at = new DateTime(2026, 10, 26, 19, 0, 0, DateTimeKind.Utc);
        var list = new[]
        {
            new Customer { Id = 37, Email = "rikkebunch@hotmaol.com", Name = "Riksi", CreatedAt = at },
            new Customer { Id = 38, Email = "rikkebunch@hotmail.com", Name = "Riksi", CreatedAt = at.AddMinutes(18) },
            new Customer { Id = 40, Email = "mette@gmail.com", Name = "Mette", CreatedAt = at },
            new Customer { Id = 41, Email = "idskou@yahoo.com", Name = "Mette", CreatedAt = at.AddDays(10) },   // another Mette, much later
            new Customer { Id = 42, Email = "ole@gmail.com", Name = "Ole", CreatedAt = at },
            new Customer { Id = 43, Email = "ole@hotmail.com", Name = "Ole J", CreatedAt = at.AddDays(30) },    // too short to say
        };
        Doubles.Find(list).Should().BeEquivalentTo(new Dictionary<int, int> { [37] = 38, [38] = 37 });
    }

    private sealed class Domains(bool? answer) : IMailDomains
    {
        public int Asked;
        public Task<bool?> TakesMailAsync(string domain, CancellationToken ct) { Asked++; return Task.FromResult(answer); }
    }

    private sealed class Factory(DbContextOptions<KunderDb> o) : IDbContextFactory<KunderDb>
    {
        public KunderDb CreateDbContext() => new(o);
    }

    private static (Kundekartotek K, SqliteConnection Conn) Kartotek(IMailDomains domains)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<KunderDb>().UseSqlite(conn).Options;
        using (var db = new KunderDb(options)) ITMartinKunder.Infrastructure.DependencyInjection.Upgrade(db);
        return (new Kundekartotek(new CustomerRepository(new Factory(options)), TimeProvider.System, domains), conn);
    }

    private static SignUpRequest Yes(string email, string name = "ole hansen") =>
        new(email, name, Companies.ITMartin, "ElPriser", "itmartin.dk: ElPriser", "Ja tak – skriv mig op", "ElPriser");

    [Test]
    public async Task A_new_address_is_checked_and_a_name_is_saved_with_capitals()
    {
        var (k, conn) = Kartotek(new Domains(true));
        using var _ = conn;
        (await k.SignUpWithCodeAsync(Yes("rikkebunch@hotmaol.com"))).Error.Should().Be("Mente du rikkebunch@hotmail.com? Ret e-mailen, og tryk igen.");
        (await k.SignUpWithCodeAsync(Yes("ole@hansen.dk"))).Error.Should().BeNull();
        (await k.ListAsync()).Single().Name.Should().Be("Ole Hansen");
    }

    [Test]
    public async Task A_domain_without_a_mail_server_is_refused_but_a_silent_DNS_stops_nobody()
    {
        var (none, c1) = Kartotek(new Domains(false));
        using (c1) (await none.SignUpWithCodeAsync(Yes("ole@findesikke-xyz.dk"))).Error.Should().Contain("mailserver for »findesikke-xyz.dk«");

        var unknown = new Domains(null);
        var (k, c2) = Kartotek(unknown);
        using (c2)
        {
            (await k.SignUpWithCodeAsync(Yes("ole@hansen.dk"))).Error.Should().BeNull();
            (await k.SignUpWithCodeAsync(Yes("ole@hansen.dk"))).Error.Should().BeNull();   // already a customer: not asked again
            unknown.Asked.Should().Be(1);
        }
    }
}
