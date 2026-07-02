using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Domain.Entities;
using ITMartinFamily.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Server.Services;

public sealed class TaskReminderService(IServiceScopeFactory scopeFactory, ILogger<TaskReminderService> logger)
    : BackgroundService
{
    private int _lastEveningReminderDay = -1;
    private int _lastMorningReminderDay  = -1;
    private int _lastCleanupDay          = -1;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                await MaybeSendEveningReminder(now, ct);
                await MaybeSendMorningReminder(now, ct);
                await MaybeRunCleanup(now, ct);
                await SendUrgentReminders(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Task reminder check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    // 22:00 — remind each member who has uncompleted tasks they claimed today
    private async Task MaybeSendEveningReminder(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 22 || _lastEveningReminderDay == now.DayOfYear) return;
        _lastEveningReminderDay = now.DayOfYear;

        using var scope = scopeFactory.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        var today = DateOnly.FromDateTime(now);

        // Group uncompleted claimed tasks by (familyId, memberName)
        var groups = await db.Set<ITMartinFamily.Domain.Entities.DailyTask>()
            .Where(t => t.Date == today && !t.CompletedAt.HasValue && t.ClaimedBy != null && t.ClaimedBy != "")
            .GroupBy(t => new { t.FamilyId, t.ClaimedBy })
            .Select(g => new { g.Key.FamilyId, Member = g.Key.ClaimedBy!, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var g in groups)
        {
            var count = g.Count;
            var body  = count == 1
                ? "Du har stadig 1 opgave du ikke har klaret i dag."
                : $"Du har stadig {count} opgaver du ikke har klaret i dag.";

            await push.SendToMemberAsync(g.FamilyId, g.Member, "📋 Uafsluttede opgaver", body);
            logger.LogInformation("Evening reminder sent to {Member} ({Count} tasks)", g.Member, count);
        }
    }

    // 08:00 — remind the whole group about tasks from yesterday that were never claimed
    private async Task MaybeSendMorningReminder(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 8 || _lastMorningReminderDay == now.DayOfYear) return;
        _lastMorningReminderDay = now.DayOfYear;

        using var scope = scopeFactory.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        var yesterday = DateOnly.FromDateTime(now.AddDays(-1));

        // Find families with unclaimed tasks from yesterday
        var groups = await db.Set<ITMartinFamily.Domain.Entities.DailyTask>()
            .Where(t => t.Date == yesterday && !t.CompletedAt.HasValue && (t.ClaimedBy == null || t.ClaimedBy == ""))
            .GroupBy(t => t.FamilyId)
            .Select(g => new { FamilyId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var g in groups)
        {
            var count = g.Count;
            var body  = count == 1
                ? "Der er 1 opgave fra i går som ingen tog."
                : $"Der er {count} opgaver fra i går som ingen tog.";

            await push.SendToFamilyAsync(g.FamilyId, "", "📋 Opgaver fra i går", body);
            logger.LogInformation("Morning reminder sent to family {FamilyId} ({Count} unclaimed)", g.FamilyId, count);
        }
    }

    // 03:00 — nightly data cleanup
    private async Task MaybeRunCleanup(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 3 || _lastCleanupDay == now.DayOfYear) return;
        _lastCleanupDay = now.DayOfYear;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
        var utcNow = DateTime.UtcNow;

        // FindIt items not touched in 90 days — delete item + photo file
        var staleItems = await db.StoredItems
            .Where(i => i.UpdatedAt < utcNow.AddDays(-90))
            .ToListAsync(ct);
        foreach (var item in staleItems)
            DeleteFile(item.PhotoPath);
        db.StoredItems.RemoveRange(staleItems);

        // Orphaned FindIt photo files (file on disk but no matching DB record)
        var photoDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "findit-photos");
        if (Directory.Exists(photoDir))
        {
            var knownPaths = await db.StoredItems
                .Where(i => i.PhotoPath != null)
                .Select(i => i.PhotoPath!)
                .ToListAsync(ct);
            foreach (var file in Directory.GetFiles(photoDir))
                if (!knownPaths.Contains(file))
                    DeleteFile(file);
        }

        // Tasks older than 30 days
        var cutoffTask = DateOnly.FromDateTime(now.AddDays(-30));
        var oldTasks = await db.Set<DailyTask>()
            .Where(t => t.Date < cutoffTask)
            .ToListAsync(ct);
        foreach (var t in oldTasks)
            DeleteFile(t.ImagePath);
        db.Set<DailyTask>().RemoveRange(oldTasks);

        // Orphaned task photo files
        var taskDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "tasks");
        if (Directory.Exists(taskDir))
        {
            var knownTaskPaths = await db.Set<DailyTask>()
                .Where(t => t.ImagePath != null)
                .Select(t => t.ImagePath!)
                .ToListAsync(ct);
            foreach (var file in Directory.GetFiles(taskDir))
                if (!knownTaskPaths.Contains(file))
                    DeleteFile(file);
        }

        // Done personal reminders older than 7 days
        var cutoffReminder = DateOnly.FromDateTime(now.AddDays(-7));
        var oldReminders = await db.Reminders
            .Where(r => r.Done && r.Date < cutoffReminder)
            .ToListAsync(ct);
        db.Reminders.RemoveRange(oldReminders);

        // Sessions older than 90 days
        var oldSessions = await db.Sessions
            .Where(s => s.CreatedAt < utcNow.AddDays(-90))
            .ToListAsync(ct);
        db.Sessions.RemoveRange(oldSessions);

        // Chat messages older than 90 days
        var oldChat = await db.Chat
            .Where(m => m.SentAt < utcNow.AddDays(-90))
            .ToListAsync(ct);
        db.Chat.RemoveRange(oldChat);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Nightly cleanup: {Items} FindIt items, {Tasks} tasks, {Reminders} reminders, {Sessions} sessions, {Chat} chat messages removed",
            staleItems.Count, oldTasks.Count, oldReminders.Count, oldSessions.Count, oldChat.Count);
    }

    // Every 5 min — send push when urgent reminder is 30 min away
    private async Task SendUrgentReminders(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        var windowStart = DateTime.UtcNow.AddMinutes(25);
        var windowEnd   = DateTime.UtcNow.AddMinutes(35);

        var due = await db.Reminders
            .Where(r => !r.Done && !r.NotificationSent
                && r.RemindAt != null
                && r.RemindAt >= windowStart && r.RemindAt <= windowEnd)
            .ToListAsync(ct);

        foreach (var r in due)
        {
            var localTime = r.RemindAt!.Value.ToLocalTime().ToString("HH:mm");
            await push.SendToMemberAsync(r.FamilyId, r.MemberName,
                $"⏰ Husk kl. {localTime}", r.Text);
            r.NotificationSent = true;
            logger.LogInformation("Urgent reminder sent to {Member}: {Text}", r.MemberName, r.Text);
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static void DeleteFile(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            try { File.Delete(path); } catch { }
    }
}
