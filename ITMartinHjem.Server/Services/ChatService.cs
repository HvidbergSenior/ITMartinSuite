using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// Live chat between a visitor and the owner. Both sides are Blazor circuits in this one
// process, so a plain event is enough to update the other screen at once.
public sealed class ChatService(IDbContextFactory<HjemDb> dbf, PushService push, ILogger<ChatService> log)
{
    public event Action<int>? Changed;            // thread id
    public event Action? AvailabilityChanged;

    public const int MaxText = 2000;

    public async Task<ChatThread?> GetAsync(string visitorKey)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking()
            .FirstOrDefaultAsync(t => t.VisitorKey == visitorKey);
    }

    public async Task<ChatThread?> GetAsync(int id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages.OrderBy(m => m.At)).AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<ChatThread>> ThreadsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Threads.Include(t => t.Messages).AsNoTracking()
            .OrderByDescending(t => t.LastAt).Take(100).ToListAsync();
    }

    public async Task VisitorWritesAsync(string visitorKey, string name, string text)
    {
        text = Trim(text);
        if (text.Length == 0 || visitorKey.Length is < 8 or > 64) return;
        name = Trim(name, 60);
        await using var db = await dbf.CreateDbContextAsync();
        var t = await db.Threads.FirstOrDefaultAsync(x => x.VisitorKey == visitorKey);
        if (t is null) { t = new ChatThread { VisitorKey = visitorKey }; db.Threads.Add(t); }
        if (name.Length > 0) t.Name = name;
        t.LastAt = DateTime.UtcNow;
        t.UnreadForOwner = true;
        t.Messages.Add(new ChatMessage { Text = text });
        await db.SaveChangesAsync();
        Changed?.Invoke(t.Id);

        var who = t.Name.Length > 0 ? t.Name : "En besøgende";
        try { await push.SendToOwnerAsync($"💬 {who} skriver", text.Length > 120 ? text[..120] + "…" : text, $"/admin?chat={t.Id}"); }
        catch (Exception ex) { log.LogWarning(ex, "Chat push failed"); }
    }

    public async Task OwnerWritesAsync(int threadId, string text)
    {
        text = Trim(text);
        if (text.Length == 0) return;
        await using var db = await dbf.CreateDbContextAsync();
        var t = await db.Threads.FirstOrDefaultAsync(x => x.Id == threadId);
        if (t is null) return;
        t.LastAt = DateTime.UtcNow;
        t.UnreadForOwner = false;
        db.Messages.Add(new ChatMessage { ThreadId = t.Id, FromOwner = true, Text = text });
        await db.SaveChangesAsync();
        Changed?.Invoke(t.Id);
    }

    public async Task MarkReadAsync(int threadId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        await db.Threads.Where(t => t.Id == threadId).ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadForOwner, false));
    }

    public async Task DeleteAsync(int threadId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        await db.Threads.Where(t => t.Id == threadId).ExecuteDeleteAsync();
        Changed?.Invoke(threadId);
    }

    public void RaiseAvailability() => AvailabilityChanged?.Invoke();

    private static string Trim(string s, int max = MaxText) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }
}
