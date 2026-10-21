using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

// Svar (/admin): what only Martin does - the texts on the front page, "Min dag", the timeline, the pages and the apps.
// The page keeps the screen flow (progress lines, the AI suggestion he picks from); every save goes through here.
public sealed class OwnerUseCases(ISettingsRepository settings, IDayNoteRepository days, IPostRepository posts, IPageRepository pages,
    IAppLinkRepository apps, IMediaStore files, ITextChecker checker, ChatUseCases chat)
{
    public const long MaxUploadBytes = 500L * 1024 * 1024;

    public Task<TextReview?> CheckTextAsync(string text, TextKind kind) => checker.CheckAsync(text, kind);

    // ── texts + availability ──
    public Task<Settings> SettingsAsync() => settings.GetAsync();

    /// <summary>The texts or pictures changed: "opdateret" moves on, and open front pages reload.</summary>
    public async Task SaveSettingsAsync(Settings s)
    {
        s.NowUpdated = DateTime.UtcNow;
        await settings.SaveAsync(s);
        chat.RaiseAvailability();
    }

    public async Task SetAvailableAsync(Settings s, bool available)
    {
        s.Available = available;
        await settings.SaveAsync(s);
        chat.RaiseAvailability();
    }

    // ── files ──
    public Task<SavedMedia> UploadAsync(Stream input, string fileName, string contentType) => files.SaveAsync(input, fileName, contentType);
    public void DeleteFile(Media m) => files.Delete(m.StoredName, m.ThumbName);

    public async Task SetNowMediaAsync(Settings s, Media m)
    {
        DeleteNowFiles(s);
        s.NowMediaName = m.StoredName;
        s.NowMediaThumb = m.ThumbName ?? "";
        s.NowMediaType = m.ContentType;
        await SaveSettingsAsync(s);
    }

    public async Task RemoveNowMediaAsync(Settings s)
    {
        DeleteNowFiles(s);
        s.NowMediaName = s.NowMediaThumb = s.NowMediaType = "";
        await SaveSettingsAsync(s);
    }

    private void DeleteNowFiles(Settings s)
    {
        if (s.NowMediaName.Length > 0) files.Delete(s.NowMediaName, s.NowMediaThumb);
    }

    // ── "Min dag" entries (pictures and lines through the day) ──
    public async Task SaveDayEntriesAsync(Settings s, IEnumerable<Settings.DayEntry> entries)
    {
        foreach (var old in s.SetEntries(entries).Where(x => x.Stored.Length > 0)) files.Delete(old.Stored, old.Thumb);
        await SaveSettingsAsync(s);
    }

    public async Task RemoveDayEntryAsync(Settings s, Settings.DayEntry e)
    {
        if (e.Stored.Length > 0) files.Delete(e.Stored, e.Thumb);
        await SaveDayEntriesAsync(s, s.Entries().Where(x => x != e));
    }

    // ── the day plan (plan in the morning, what it became in the evening) ──
    public async Task<DayNote> TodayAsync()
    {
        var day = DayNote.Key(Dk.Now);
        return await days.GetAsync(day) ?? new DayNote { Day = day };
    }

    /// <returns>Null when Svar was left open past midnight: yesterday's text stays on yesterday, today starts empty.</returns>
    public async Task<DayNote?> SaveTodayAsync(DayNote note)
    {
        var day = DayNote.Key(Dk.Now);
        if (note.Day != day) return null;
        return await days.SaveAsync(day, note.Plan.Trim(), note.Done.Trim());
    }

    // ── timeline ──
    public Task<List<Post>> PostsAsync() => posts.TimelineAsync(family: true);
    public Task<Post?> PostAsync(int id) => posts.GetAsync(id);

    public async Task SavePostAsync(Post edit, IReadOnlyCollection<Media> removed)
    {
        await posts.SaveAsync(edit, removed.Where(m => m.Id != 0).Select(m => m.Id).ToList());
        foreach (var r in removed) DeleteFile(r);
    }

    /// <summary>A draft that was never saved: its uploaded files go again.</summary>
    public void DropDraft(Post draft)
    {
        foreach (var m in draft.Media.Where(m => m.Id == 0)) DeleteFile(m);
    }

    public async Task DeletePostAsync(int id)
    {
        if (await posts.DeleteAsync(id) is { } gone)
            foreach (var m in gone.Media) DeleteFile(m);
    }

    // ── pages ──
    public Task<List<Page>> PagesAsync() => pages.AllAsync();

    /// <returns>Error for Martin (Danish), or null when saved - the edit then carries the id and the cleaned address.</returns>
    public async Task<string?> SavePageAsync(Page edit)
    {
        var slug = PageRules.NormalizeSlug(edit.Slug);
        if (slug.Length == 0 || edit.Title.Trim().Length == 0) return "Siden skal have en titel og en adresse.";
        if (PageRules.IsReserved(slug)) return $"Adressen \"{slug}\" bruges af appen selv – vælg en anden.";
        if (await pages.SlugTakenAsync(slug, edit.Id)) return "Der findes allerede en side med den adresse.";
        edit.Slug = slug;
        edit.Title = edit.Title.Trim();
        edit.UpdatedAt = DateTime.UtcNow;
        edit.Id = await pages.SaveAsync(edit);
        return null;
    }

    public Task DeletePageAsync(int id) => pages.DeleteAsync(id);

    // ── apps on "Mine apps" ──
    public Task<List<AppLink>> AppsAsync() => apps.AllAsync();

    /// <summary>Only rows with a name and an address are saved.</summary>
    public Task SaveAppsAsync(IEnumerable<AppLink> links) => apps.SaveAsync(links.Where(a => a.IsComplete));
}
