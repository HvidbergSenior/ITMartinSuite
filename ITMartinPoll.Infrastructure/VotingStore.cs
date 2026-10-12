using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Infrastructure;

// Voting and the Kundekartotek on PollDb. A short-lived DbContext per call (Blazor Server keeps scoped services for the whole visit).
public sealed class VotingStore(IDbContextFactory<PollDb> dbf) : IVotingStore, ICustomerStore
{
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
            db.Votes.Add(new Vote
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

    // New or returning customer: the newest consent wins, interests are added, and an earlier unsubscribe is lifted (they said yes again).
    private static void Apply(PollDb db, Customer? c, CustomerOptIn o)
    {
        if (c is null) db.Customers.Add(c = new Customer { Email = o.Email, CreatedAt = o.AtUtc });
        if (o.Name.Length > 0) c.Name = o.Name;
        c.Source = o.Source; c.ConsentText = o.ConsentText; c.ConsentAt = o.AtUtc;
        c.WantsAuctionNews = true; c.UnsubscribedAt = null;
        c.Interests = c.Interests.Length == 0 || c.Interests == o.Interests ? o.Interests : c.Interests + " ; " + o.Interests;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPollInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<PollDb>(o => o.UseSqlite(connectionString));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PollDb>>().CreateDbContext());   // pages that still read PollDb directly
        services.AddSingleton<VotingStore>();
        services.AddSingleton<IVotingStore>(sp => sp.GetRequiredService<VotingStore>());
        services.AddSingleton<ICustomerStore>(sp => sp.GetRequiredService<VotingStore>());
        return services;
    }

    /// <summary>Creates or upgrades poll.db (and seeds the demo when asked) - once at start.</summary>
    public static async Task UpgradePollDatabaseAsync(this IServiceProvider services, bool seedDemo)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<PollDb>>().CreateDbContextAsync();
        PollSchema.Upgrade(db);
        if (seedDemo) await DemoSeeder.SeedAsync(db);
    }
}
