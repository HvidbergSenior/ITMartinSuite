using System.Text.RegularExpressions;
using ITMartinAdhd.Application;
using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinAdhd.Infrastructure;

public sealed class EfItemStore(AdhdDbContext db) : IItemStore
{
    public Task<List<StoredItem>> RecentAsync(int count, CancellationToken ct = default) =>
        db.StoredItems.OrderByDescending(i => i.UpdatedAt).Take(count).ToListAsync(ct);

    public Task<List<StoredItem>> AllAsync(CancellationToken ct = default) => db.StoredItems.ToListAsync(ct);

    public async Task<StoredItem?> GetAsync(int id, CancellationToken ct = default) => await db.StoredItems.FindAsync([id], ct);

    public async Task AddAsync(StoredItem item, CancellationToken ct = default)
    {
        db.StoredItems.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(IEnumerable<StoredItem> items, CancellationToken ct = default)
    {
        db.StoredItems.RemoveRange(items);
        await db.SaveChangesAsync(ct);
    }
}

// <data>/photos/<guid>.<ext>. Older rows hold "/photos/<id>.jpg" - only the file name part is ever used.
public sealed partial class FilePhotoStore : IPhotoStore
{
    private readonly string _dir;

    public FilePhotoStore(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(_dir);
    }

    public async Task<string> SaveAsync(Stream photo, string extension, CancellationToken ct = default)
    {
        var name = Guid.NewGuid() + extension;
        await using var fs = File.Create(Path.Combine(_dir, name));
        await photo.CopyToAsync(fs, ct);
        return name;
    }

    public string? PathOf(string? name)
    {
        var file = SafeName(name);
        if (file is null) return null;
        var full = Path.Combine(_dir, file);
        return File.Exists(full) ? full : null;
    }

    public void Delete(string? name)
    {
        if (PathOf(name) is { } full) File.Delete(full);
    }

    private static string? SafeName(string? name)
    {
        var file = Path.GetFileName(name ?? "");
        return PhotoName().IsMatch(file) ? file : null;
    }

    [GeneratedRegex(@"^[0-9A-Za-z-]{1,40}\.(jpg|jpeg|png|webp|heic)$", RegexOptions.IgnoreCase)]
    private static partial Regex PhotoName();
}

// Once a day: things untouched for 30 days are cleared away (KeepRule).
public sealed class StaleCleanup(IServiceScopeFactory scopes, ILogger<StaleCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<Things>().ClearStaleAsync(ct);
                if (n > 0) log.LogInformation("Find det igen: cleared {Count} things untouched for 30 days", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Clean-up failed"); }
            try { await Task.Delay(TimeSpan.FromHours(24), ct); } catch (OperationCanceledException) { }
        }
    }
}
