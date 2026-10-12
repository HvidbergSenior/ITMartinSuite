using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Infrastructure;

// Row shapes of the existing tables (created by the flat Server's EnsureCreated + raw SQL) - only the columns voting needs.
internal sealed class PollRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime? Deadline { get; set; }
    public bool IsActive { get; set; }
    public bool AllowMultiple { get; set; }
    public bool RequireContact { get; set; }
}

internal sealed class OptionRow
{
    public int Id { get; set; }
    public int PollId { get; set; }
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
}

internal sealed class VoteRow
{
    public int Id { get; set; }
    public int PollId { get; set; }
    public int OptionId { get; set; }
    public string Comment { get; set; } = "";
    public string VoterName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool WantsAuctionNews { get; set; }
    public DateTime VotedAt { get; set; }
}

internal sealed class CustomerRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Source { get; set; } = "";
    public string ConsentText { get; set; } = "";
    public DateTime ConsentAt { get; set; }
    public bool WantsAuctionNews { get; set; }
    public string UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? UnsubscribedAt { get; set; }
    public string Note { get; set; } = "";
    public string Interests { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class VotingDb(DbContextOptions<VotingDb> options) : DbContext(options)
{
    internal DbSet<PollRow> Polls => Set<PollRow>();
    internal DbSet<OptionRow> Options => Set<OptionRow>();
    internal DbSet<VoteRow> Votes => Set<VoteRow>();
    internal DbSet<CustomerRow> Customers => Set<CustomerRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PollRow>().ToTable("Polls");
        b.Entity<OptionRow>().ToTable("Options");
        b.Entity<VoteRow>().ToTable("Votes");
        b.Entity<CustomerRow>().ToTable("Customers");
    }
}

// A short-lived DbContext per call (Blazor Server keeps scoped services for the whole visit).
public sealed class VotingStore(IDbContextFactory<VotingDb> dbf) : IVotingStore, ICustomerStore
{
    public async Task<bool> UnsubscribeAsync(string email, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (c is null) return false;
        if (c.UnsubscribedAt is null) { c.UnsubscribedAt = nowUtc; c.WantsAuctionNews = false; await db.SaveChangesAsync(ct); }
        return true;
    }

    public async Task UpsertAsync(CustomerOptIn o, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        Apply(db, await db.Customers.FirstOrDefaultAsync(x => x.Email == o.Email, ct), o);
        await db.SaveChangesAsync(ct);
    }

    // New or returning customer: the newest consent and interests win, and an earlier unsubscribe is lifted (they said yes again).
    private static void Apply(VotingDb db, CustomerRow? c, CustomerOptIn o)
    {
        if (c is null) db.Customers.Add(c = new CustomerRow { Email = o.Email, CreatedAt = o.AtUtc });
        if (o.Name.Length > 0) c.Name = o.Name;
        c.Source = o.Source; c.ConsentText = o.ConsentText; c.ConsentAt = o.AtUtc;
        c.WantsAuctionNews = true; c.UnsubscribedAt = null;
        c.Interests = c.Interests.Length == 0 || c.Interests == o.Interests ? o.Interests : c.Interests + " ; " + o.Interests;
    }

    public async Task<Ballot?> GetAsync(int pollId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var p = await db.Polls.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pollId, ct);
        if (p is null) return null;
        var options = await db.Options.AsNoTracking().Where(o => o.PollId == pollId).ToListAsync(ct);
        var votes = await db.Votes.AsNoTracking().Where(v => v.PollId == pollId).ToListAsync(ct);
        return new Ballot(p.Id, p.Title, p.Body, p.AllowMultiple, p.RequireContact, p.IsActive,
            p.Deadline?.ToUniversalTime(),   // stored as local time, read the same way as Poll.HasDeadlinePassed
            options.Select(o => new BallotOption(o.Id, o.Label, o.SortOrder)).ToList(),
            votes.Select(v => new CastVote(v.OptionId, v.VoterName, v.Email, DateTime.SpecifyKind(v.VotedAt, DateTimeKind.Utc))).ToList());
    }

    public async Task SaveAsync(int pollId, AcceptedVote vote, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var first = true;
        foreach (var r in vote.Rows)
        {
            db.Votes.Add(new VoteRow
            {
                PollId = pollId, OptionId = r.OptionId, VoterName = r.VoterName, Email = r.Email, VotedAt = r.VotedAtUtc,
                Comment = first ? vote.Comment : "",   // the comment belongs to the person, keep it once
                WantsAuctionNews = vote.WantsAuctionNews,
            });
            first = false;
        }
        if (vote.OptIn is { } o) Apply(db, await db.Customers.FirstOrDefaultAsync(x => x.Email == o.Email, ct), o);
        await db.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPollInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<VotingDb>(o => o.UseSqlite(connectionString));
        services.AddSingleton<VotingStore>();
        services.AddSingleton<IVotingStore>(sp => sp.GetRequiredService<VotingStore>());
        services.AddSingleton<ICustomerStore>(sp => sp.GetRequiredService<VotingStore>());
        return services;
    }
}
