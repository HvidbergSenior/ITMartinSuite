using ITMartinAuction.Application;
using ITMartinAuction.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAuction.Infrastructure;

public sealed class AuctionDbContext(DbContextOptions<AuctionDbContext> options) : DbContext(options)
{
    public DbSet<AuctionSession> Sessions => Set<AuctionSession>();
    public DbSet<AuctionItem> Items => Set<AuctionItem>();
    public DbSet<Bidder> Bidders => Set<Bidder>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AuctionSession>().HasIndex(s => s.Code).IsUnique();
        // The relations by the real column, SessionId. Without this EF made a hidden "AuctionSessionId" that was never
        // filled in, so an auction loaded with its items, bidders and chat came back empty (found 2026-10-12; the
        // old column stays in existing databases, unused).
        model.Entity<AuctionSession>().HasMany(s => s.Items).WithOne().HasForeignKey(i => i.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<AuctionSession>().HasMany(s => s.Bidders).WithOne().HasForeignKey(b => b.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<AuctionSession>().HasMany(s => s.ChatMessages).WithOne().HasForeignKey(c => c.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<AuctionItem>().Property(i => i.StartingPrice).HasColumnType("TEXT");
        model.Entity<AuctionItem>().Property(i => i.WinningBid).HasColumnType("TEXT");
        model.Entity<AuctionItem>().Ignore(i => i.IsHighlighted);
        model.Entity<Bid>().Property(b => b.Amount).HasColumnType("TEXT");
    }
}

// The store: one DbContext per scope (Blazor circuit / request), so what a use case loads is tracked until SaveAsync.
public sealed class AuctionStore(AuctionDbContext db) : IAuctionStore
{
    public void Add(AuctionSession session) => db.Sessions.Add(session);
    public void Add(AuctionItem item) => db.Items.Add(item);
    public void Add(Bidder bidder) => db.Bidders.Add(bidder);
    public void Add(Bid bid) => db.Bids.Add(bid);
    public void Add(ChatMessage message) => db.ChatMessages.Add(message);
    public void Remove(IEnumerable<Bid> bids) => db.Bids.RemoveRange(bids);
    public void Remove(IEnumerable<AuctionSession> sessions) => db.Sessions.RemoveRange(sessions);

    public Task<AuctionSession?> SessionAsync(string code, CancellationToken ct = default) =>
        db.Sessions.FirstOrDefaultAsync(s => s.Code == code, ct);

    public Task<AuctionSession?> SessionWithBiddersAsync(string code, CancellationToken ct = default) =>
        db.Sessions.Include(s => s.Bidders).FirstOrDefaultAsync(s => s.Code == code, ct);

    public Task<AuctionSession?> SessionWithItemsAsync(string code, CancellationToken ct = default) =>
        db.Sessions.Include(s => s.Items).FirstOrDefaultAsync(s => s.Code == code, ct);

    public Task<AuctionSession?> SessionFullAsync(string code, bool withChat = false, CancellationToken ct = default)
    {
        var q = db.Sessions.Include(s => s.Bidders).Include(s => s.Items).ThenInclude(i => i.Bids).ThenInclude(b => b.Bidder).AsQueryable();
        if (withChat) q = q.Include(s => s.ChatMessages);
        return q.FirstOrDefaultAsync(s => s.Code == code, ct);
    }

    public Task<AuctionItem?> ItemAsync(Guid id, bool withBids = false, bool withBidders = false, CancellationToken ct = default)
    {
        var q = db.Items.AsQueryable();
        if (withBidders) q = q.Include(i => i.Bids).ThenInclude(b => b.Bidder);
        else if (withBids) q = q.Include(i => i.Bids);
        return q.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public Task<List<AuctionItem>> ItemsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.Items.Where(i => ids.Contains(i.Id)).ToListAsync(ct);

    public Task<List<Bidder>> BiddersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.Bidders.Where(b => ids.Contains(b.Id)).ToListAsync(ct);

    public async Task<int> MaxBidderNumberAsync(Guid sessionId, CancellationToken ct = default) =>
        await db.Bidders.Where(b => b.SessionId == sessionId && b.BidderNumber.HasValue).MaxAsync(b => (int?)b.BidderNumber, ct) ?? 0;

    public Task<List<ChatMessage>> ChatAsync(string code, CancellationToken ct = default) =>
        db.ChatMessages.Join(db.Sessions, c => c.SessionId, s => s.Id, (c, s) => new { c, s.Code })
            .Where(x => x.Code == code).Select(x => x.c).OrderBy(c => c.SentAt).ToListAsync(ct);

    public Task<List<AuctionSession>> OpenSessionsAsync(CancellationToken ct = default) =>
        db.Sessions.Include(s => s.Items).Where(s => s.Status != AuctionStatus.Ended).ToListAsync(ct);

    public Task<List<AuctionSession>> ExpiredSessionsAsync(DateTime now, CancellationToken ct = default) =>
        db.Sessions.Where(s => s.ExpiresAt < now).ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public static class AuctionSchema
{
    // EnsureCreated builds a new database; columns added after the first deploy are bolted on here.
    private static readonly string[] Migrations =
    [
        "ALTER TABLE Sessions ADD COLUMN Status INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Sessions ADD COLUMN AdminToken TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE Sessions ADD COLUMN AuctionDate TEXT",
        "ALTER TABLE Items ADD COLUMN LotQuantity INTEGER NOT NULL DEFAULT 1",
        "ALTER TABLE Items ADD COLUMN BuyNowBuyerName TEXT",
        "ALTER TABLE Items ADD COLUMN BuyNowBuyerPhone TEXT",
        "ALTER TABLE Items ADD COLUMN Highlight TEXT",   // 2026-10-12: guld/sølv/bronze/måske from Bogshoppen Lager
        "ALTER TABLE Bidders ADD COLUMN Token TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE Bidders ADD COLUMN BidderNumber INTEGER",
        "ALTER TABLE Bidders ADD COLUMN Phone TEXT",
        "ALTER TABLE Bids ADD COLUMN IsPreBid INTEGER NOT NULL DEFAULT 0",
        """
        CREATE TABLE IF NOT EXISTS ChatMessages (
            Id TEXT NOT NULL PRIMARY KEY, SessionId TEXT NOT NULL, BidderNumber INTEGER NOT NULL,
            Message TEXT NOT NULL, SentAt TEXT NOT NULL)
        """,
    ];

    public static async Task UpgradeAsync(AuctionDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        foreach (var sql in Migrations)
        {
            try { cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && (ex.Message.Contains("duplicate column") || ex.Message.Contains("already exists"))) { }
        }
    }
}

// Minimal demo seed - one live-ish auction with a couple of items and bids. Only with Auction:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(AuctionDbContext db)
    {
        if (await db.Sessions.AnyAsync()) return;
        var session = AuctionSession.Open("Klub-auktion", "DEMO", DateTime.UtcNow);
        session.Status = AuctionStatus.Live;
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        var item1 = new AuctionItem { SessionId = session.Id, Name = "Håndlavet fuglekasse", StartingPrice = 50m, Status = AuctionItemStatus.Active, SortOrder = 0 };
        var item2 = new AuctionItem { SessionId = session.Id, Name = "Gavekort til lokal café", StartingPrice = 100m, Status = AuctionItemStatus.Pending, SortOrder = 1 };
        db.Items.AddRange(item1, item2);
        await db.SaveChangesAsync();
        var anna = new Bidder { SessionId = session.Id, Name = "Anna", BidderNumber = 1, Color = "#f5a623" };
        var bo = new Bidder { SessionId = session.Id, Name = "Bo", BidderNumber = 2, Color = "#4f8ef7" };
        db.Bidders.AddRange(anna, bo);
        await db.SaveChangesAsync();
        db.Bids.AddRange(new Bid { AuctionItemId = item1.Id, BidderId = anna.Id, Amount = 55m, PlacedAt = session.CreatedAt }, new Bid { AuctionItemId = item1.Id, BidderId = bo.Id, Amount = 65m, PlacedAt = session.CreatedAt.AddSeconds(5) });
        session.ActiveItemId = item1.Id;
        await db.SaveChangesAsync();
    }
}
