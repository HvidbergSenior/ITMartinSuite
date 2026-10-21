using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ITMartinKunder.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Infrastructure;

// Voting on PollDb. A short-lived DbContext per call (Blazor Server keeps scoped services for the whole visit).
// The Kundekartotek is Kunder since 2026-10-21 (KunderCustomerStore); poll.db Customers is only the old list, moved once.
public sealed class VotingStore(IDbContextFactory<PollDb> dbf) : IVotingStore
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
        await db.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    /// <param name="dataDir">Where the Kunder outbox and the "old list moved" mark live (/app/data).</param>
    public static IServiceCollection AddPollInfrastructure(this IServiceCollection services, string connectionString, string? kunderUrl,
        string? kunderApiKey, string dataDir = "/app/data", string imagesDir = "/app/data/images")
    {
        services.AddDbContextFactory<PollDb>(o => o.UseSqlite(connectionString));
        static void Store<T>(IServiceCollection s, Func<PollDb, IQueryable<T>> whole) where T : class =>
            s.AddSingleton<IAggregateStore<T>>(sp => new EfAggregateStore<T>(sp.GetRequiredService<IDbContextFactory<PollDb>>(), whole));
        Store<Poll>(services, Aggregates.Poll);
        Store<ImageSession>(services, Aggregates.Session);
        Store<DatePoll>(services, Aggregates.DatePoll);
        Store<Package>(services, Aggregates.Package);
        Store<HelpEvent>(services, Aggregates.Help);
        Store<Customer>(services, Aggregates.Customer);
        services.AddSingleton<IImageFiles>(new FileImageStore(imagesDir));
        services.AddSingleton<VotingStore>();
        services.AddSingleton<IVotingStore>(sp => sp.GetRequiredService<VotingStore>());
        services.AddKunderClient(kunderUrl, kunderApiKey, dataDir);
        services.AddScoped<ICustomerStore, KunderCustomerStore>();
        services.AddHostedService(sp => new MoveOldCustomersJob(sp, Path.Combine(dataDir, "kunder-flyttet.txt")));
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
