using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Infrastructure.Persistence;

// One store for every aggregate: "whole" says which children belong to it. A short-lived DbContext per call
// (Blazor Server keeps scoped services for the whole visit). Ids are database-made ints, so children added to a
// loaded aggregate's lists are inserted, and children removed from them are deleted (required relationships).
public sealed class EfAggregateStore<T>(IDbContextFactory<PollDb> dbf, Func<PollDb, IQueryable<T>> whole) : IAggregateStore<T> where T : class
{
    public async Task<T?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await whole(db).AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);
    }

    public async Task<List<T>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await whole(db).AsNoTracking().ToListAsync(ct);
    }

    public async Task<T> AddAsync(T aggregate, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Add(aggregate);
        await db.SaveChangesAsync(ct);
        return aggregate;
    }

    public async Task<bool> ChangeAsync(int id, Func<T, Task> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var e = await whole(db).FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);
        if (e is null) return false;
        await change(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var e = await whole(db).FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);
        if (e is null) return false;
        db.Remove(e);   // the loaded children go with it, also in tables made without a foreign key
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public static class Aggregates
{
    public static IQueryable<Poll> Poll(PollDb db) => db.Polls.Include(p => p.Options).Include(p => p.Votes).AsSplitQuery();
    public static IQueryable<ImageSession> Session(PollDb db) => db.Sessions.Include(s => s.Images).ThenInclude(i => i.Ratings).AsSplitQuery();
    public static IQueryable<DatePoll> DatePoll(PollDb db) => db.DatePolls
        .Include(p => p.Dates).ThenInclude(d => d.Responses).Include(p => p.Images).Include(p => p.ChatMessages).Include(p => p.Hosts).AsSplitQuery();
    public static IQueryable<Package> Package(PollDb db) => db.Packages
        .Include(p => p.Polls).ThenInclude(x => x.Options).Include(p => p.Sessions).ThenInclude(s => s.Images).AsSplitQuery();
    public static IQueryable<HelpEvent> Help(PollDb db) => db.HelpEvents
        .Include(e => e.Slots).Include(e => e.Tasks).Include(e => e.Signups).Include(e => e.Chat).AsSplitQuery();
    public static IQueryable<Customer> Customer(PollDb db) => db.Customers;
}

// Uploaded pictures in /app/data/images (Poll:ImagesDir), new random name, original extension.
public sealed class FileImageStore(string dir) : IImageFiles
{
    public async Task<string> SaveAsync(Stream content, string originalName, string prefix = "", CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var name = $"{prefix}{Guid.NewGuid():N}{Path.GetExtension(originalName).ToLowerInvariant()}";
        await using var fs = File.Create(Path.Combine(dir, name));
        await content.CopyToAsync(fs, ct);
        return name;
    }
}
