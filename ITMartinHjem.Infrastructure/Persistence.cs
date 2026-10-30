using ITMartinKunder.Client;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Infrastructure;

// Only the tables that have moved out of the flat Server live here (so far: PublicQuestions).
public sealed class HjemQuestionsDb(DbContextOptions<HjemQuestionsDb> options) : DbContext(options)
{
    public DbSet<PublicQuestion> PublicQuestions => Set<PublicQuestion>();
    public DbSet<Feedback> Feedback => Set<Feedback>();   // Kritik (2026-10-29)
    public DbSet<CollabFile> CollabFiles => Set<CollabFile>();   // Samarbejde (2026-10-30)
    public DbSet<CollabNote> CollabNotes => Set<CollabNote>();
    public DbSet<CollabMessage> CollabMessages => Set<CollabMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CollabFile>().ToTable("CollabFiles").HasKey(x => x.Id);
        b.Entity<CollabNote>().ToTable("CollabNotes").HasKey(x => x.Id);
        b.Entity<CollabMessage>().ToTable("CollabMessages").HasKey(x => x.Id);

        var q = b.Entity<PublicQuestion>();
        q.ToTable("PublicQuestions");
        q.HasKey(x => x.Id);
        q.Property(x => x.Question).IsRequired();
        q.Property(x => x.Answer).IsRequired();
        q.Property(x => x.Topic).IsRequired();
        q.Property(x => x.Site).IsRequired();

        var f = b.Entity<Feedback>();
        f.ToTable("Feedback");
        f.HasKey(x => x.Id);
    }

    // hjem.db already exists (EnsureCreated by the Server), so this table is added by hand - idempotent.
    public static async Task EnsureTablesAsync(HjemQuestionsDb db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PublicQuestions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PublicQuestions" PRIMARY KEY AUTOINCREMENT,
                "Question" TEXT NOT NULL, "Answer" TEXT NOT NULL, "Topic" TEXT NOT NULL,
                "PublishedAt" TEXT NOT NULL, "SourceThreadId" INTEGER NULL,
                "Site" TEXT NOT NULL DEFAULT 'itmartin')
            """);
        // Added 2026-10-12 (questions per site) - a table made before that gets the column here.
        // Checked first (2026-10-28), so a normal start no longer logs a failed ALTER TABLE.
        var has = await db.Database.SqlQueryRaw<int>("""SELECT COUNT(*) AS "Value" FROM pragma_table_info('PublicQuestions') WHERE name = 'Site'""").SingleAsync();
        if (has == 0) await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "PublicQuestions" ADD COLUMN "Site" TEXT NOT NULL DEFAULT 'itmartin'""");
        // Kritik (2026-10-29).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Feedback" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Feedback" PRIMARY KEY AUTOINCREMENT,
                "Kind" TEXT NOT NULL, "Text" TEXT NOT NULL, "Name" TEXT NOT NULL, "MayShow" INTEGER NOT NULL,
                "Shown" INTEGER NOT NULL, "Reply" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL)
            """);
        // Samarbejde (2026-10-30).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CollabFiles" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CollabFiles" PRIMARY KEY AUTOINCREMENT,
                "Space" TEXT NOT NULL, "Room" TEXT NOT NULL, "Title" TEXT NOT NULL, "FileName" TEXT NOT NULL,
                "StoredName" TEXT NOT NULL, "ContentType" TEXT NOT NULL, "Size" INTEGER NOT NULL, "By" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL)
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CollabNotes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CollabNotes" PRIMARY KEY AUTOINCREMENT,
                "FileId" INTEGER NOT NULL, "By" TEXT NOT NULL, "Text" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL)
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CollabMessages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CollabMessages" PRIMARY KEY AUTOINCREMENT,
                "Space" TEXT NOT NULL, "By" TEXT NOT NULL, "Text" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL)
            """);
    }
}

// A short-lived DbContext per call: Blazor Server keeps scoped services for the whole visit, so a shared
// context would keep showing the Q&As as they were when the page opened.
public sealed class PublicQuestionStore(IDbContextFactory<HjemQuestionsDb> dbf) : IPublicQuestionStore
{
    public async Task<List<PublicQuestion>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.PublicQuestions.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(PublicQuestion q, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.PublicQuestions.Add(q);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(int id, Action<PublicQuestion> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = await db.PublicQuestions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return false;
        change(q);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.PublicQuestions.Where(q => q.Id == id).ExecuteDeleteAsync(ct);
    }
}

public sealed class FeedbackStore(IDbContextFactory<HjemQuestionsDb> dbf) : IFeedbackStore
{
    public async Task<List<Feedback>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Feedback.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(Feedback f, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Feedback.Add(f);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(int id, Action<Feedback> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var f = await db.Feedback.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return false;
        change(f);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Feedback.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
    }
}

public sealed class CollabStore(IDbContextFactory<HjemQuestionsDb> dbf) : ICollabStore
{
    public async Task<List<CollabFile>> FilesAsync(string space, string room, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.CollabFiles.AsNoTracking().Where(f => f.Space == space && f.Room == room).ToListAsync(ct);
    }

    public async Task<CollabFile?> FileAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.CollabFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task AddFileAsync(CollabFile f, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.CollabFiles.Add(f);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFileAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.CollabNotes.Where(n => n.FileId == id).ExecuteDeleteAsync(ct);
        await db.CollabFiles.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<List<CollabNote>> NotesAsync(IReadOnlyCollection<int> fileIds, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.CollabNotes.AsNoTracking().Where(n => fileIds.Contains(n.FileId)).ToListAsync(ct);
    }

    public async Task AddNoteAsync(CollabNote n, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.CollabNotes.Add(n);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<CollabMessage>> MessagesAsync(string space, int max, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var newest = await db.CollabMessages.AsNoTracking().Where(m => m.Space == space).OrderByDescending(m => m.Id).Take(max).ToListAsync(ct);
        newest.Reverse();
        return newest;
    }

    public async Task AddMessageAsync(CollabMessage m, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.CollabMessages.Add(m);
        await db.SaveChangesAsync(ct);
    }
}

// <DataDir>/samarbejde/<space>/<random>.<ending> - the random name means a guessed address never finds a file.
public sealed class CollabDisk(HjemInfraOptions o) : ICollabDisk
{
    private string Dir(string space) => Path.Combine(o.DataDir, "samarbejde", Path.GetFileName(space));

    public async Task<string> SaveAsync(string space, Stream input, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Dir(space));
        var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName).ToLowerInvariant();
        await using var file = File.Create(Path.Combine(Dir(space), stored));
        await input.CopyToAsync(file, ct);
        return stored;
    }

    public string? Find(string space, string storedName)
    {
        var path = Path.Combine(Dir(space), Path.GetFileName(storedName));
        return storedName.Length > 0 && File.Exists(path) ? path : null;
    }

    public void Delete(string space, string storedName)
    {
        if (Find(space, storedName) is { } path) File.Delete(path);
    }
}

public static class DependencyInjection
{
    /// <param name="stemApi">Stem (poll-web) base address, reached over the NAS docker network.</param>
    public static IServiceCollection AddHjemInfrastructure(this IServiceCollection services, string connectionString, HjemInfraOptions options, string? stemApi = null)
    {
        // The site itself (moved out of the flat Server 2026-10-20).
        services.AddSingleton(options);
        services.AddDbContextFactory<HjemDb>(o => o.UseSqlite(connectionString));
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IDayNoteRepository, DayNoteRepository>();
        services.AddSingleton<IPageRepository, PageRepository>();
        services.AddSingleton<IAppLinkRepository, AppLinkRepository>();
        services.AddSingleton<IPostRepository, PostRepository>();
        services.AddSingleton<IChatRepository, ChatRepository>();
        services.AddSingleton<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddSingleton<IHitRepository, HitRepository>();
        services.AddSingleton<IMediaStore, MediaStore>();
        services.AddSingleton<IPushSender, WebPushSender>();
        services.AddSingleton<IMailSender, SmtpMailSender>();
        services.AddSingleton<ITextChecker, ClaudeTextChecker>();   // AI proofreading of the owner's own text in Svar
        services.AddSingleton<IVisitorHasher, SaltedHasher>();
        services.AddSingleton<INeighbourApps, NeighbourApps>();
        services.AddHttpClient(AppStatusChecker.Client, c => c.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<AppStatusChecker>();
        services.AddSingleton<IAppStatus>(sp => sp.GetRequiredService<AppStatusChecker>());
        services.AddHostedService(sp => sp.GetRequiredService<AppStatusChecker>());

        services.AddHttpClient<StemGateway>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(stemApi) ? "http://poll-web:8080" : stemApi);
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddTransient<IPollGateway>(sp => sp.GetRequiredService<StemGateway>());
        services.AddKunderClient(options.KunderUrl, options.KunderApiKey, options.DataDir);
        services.AddTransient<ICustomerGateway, KunderGateway>();
        services.AddDbContextFactory<HjemQuestionsDb>(o => o.UseSqlite(connectionString));
        services.AddSingleton<IPublicQuestionStore, PublicQuestionStore>();
        services.AddSingleton<IFeedbackStore, FeedbackStore>();
        services.AddSingleton<ICollabStore, CollabStore>();
        services.AddSingleton<ICollabDisk, CollabDisk>();
        return services;
    }

    /// <summary>Creates hjem.db or adds the tables and columns that came later - once at start.</summary>
    public static async Task UpgradeHjemTablesAsync(this IServiceProvider services)
    {
        await using (var site = await services.GetRequiredService<IDbContextFactory<HjemDb>>().CreateDbContextAsync())
            await Schema.EnsureAsync(site);
        await using var db = await services.GetRequiredService<IDbContextFactory<HjemQuestionsDb>>().CreateDbContextAsync();
        await HjemQuestionsDb.EnsureTablesAsync(db);
    }
}
