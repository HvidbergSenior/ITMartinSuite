using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

public interface ICollabStore
{
    Task<List<CollabFile>> FilesAsync(string space, string room, CancellationToken ct = default);
    Task<CollabFile?> FileAsync(int id, CancellationToken ct = default);
    Task AddFileAsync(CollabFile f, CancellationToken ct = default);
    /// <summary>The file row and its thoughts.</summary>
    Task DeleteFileAsync(int id, CancellationToken ct = default);
    Task<List<CollabNote>> NotesAsync(IReadOnlyCollection<int> fileIds, CancellationToken ct = default);
    Task AddNoteAsync(CollabNote n, CancellationToken ct = default);
}

// The uploaded files themselves, under <DataDir>/samarbejde/<space>/.
public interface ICollabDisk
{
    /// <summary>Saves the file under a new random name with the same ending; returns that name.</summary>
    Task<string> SaveAsync(string space, Stream input, string fileName, CancellationToken ct = default);
    string? Find(string space, string storedName);
    void Delete(string space, string storedName);
}

/// <summary>The corners set up in appsettings.json / magic.env (Hjem__Samarbejde__0__Slug / __Name / __Code).
/// No code = open to anyone with the link (user 2026-10-30: "Dont need password for Per part yet... When we use it I will add pw").</summary>
public sealed class CollabSpaces(IEnumerable<CollabSpace> spaces)
{
    private readonly List<CollabSpace> _all = spaces.Where(s => s.Slug.Length > 0 && s.Name.Length > 0)
        .Select(s => s with { Slug = s.Slug.Trim().ToLowerInvariant() }).ToList();

    public CollabSpace? Find(string? slug) => _all.FirstOrDefault(s => s.Slug == (slug ?? "").Trim().ToLowerInvariant());
}

public sealed record CollabItem(CollabFile File, List<CollabNote> Notes);

// "Samarbejde": Martin and one other person upload texts or music and write their thoughts under each file.
// What the other person adds lands in the chat in Svar (IOwnerInbox), so Martin sees it at once.
public sealed class CollabUseCases(ICollabStore store, ICollabDisk disk, IOwnerInbox inbox, TimeProvider clock)
{
    /// <summary>Newest file first, its thoughts oldest first.</summary>
    public async Task<List<CollabItem>> ItemsAsync(string space, string room, CancellationToken ct = default)
    {
        var files = (await store.FilesAsync(space, room, ct)).OrderByDescending(f => f.CreatedAt).ToList();
        var notes = await store.NotesAsync(files.Select(f => f.Id).ToList(), ct);
        return files.Select(f => new CollabItem(f, notes.Where(n => n.FileId == f.Id).OrderBy(n => n.CreatedAt).ToList())).ToList();
    }

    /// <summary>Null when saved, else the reason in plain Danish.</summary>
    public async Task<string?> UploadAsync(string space, string room, string? title, string by, Stream input, string fileName,
        string contentType, long size, CancellationToken ct = default)
    {
        if (CollabRules.CheckFile(room, fileName, size) is { } why) return why;
        var stored = await disk.SaveAsync(space, input, fileName, ct);
        var f = CollabFile.Add(space, room, title, fileName, stored, contentType, size, by, clock.GetUtcNow().UtcDateTime);
        await store.AddFileAsync(f, ct);
        await TellMartinAsync(by, $"{by} har lagt \"{f.Title}\" på /{space}/{room}.", ct);
        return null;
    }

    public async Task<string?> WriteAsync(int fileId, string by, string? text, CancellationToken ct = default)
    {
        if (await store.FileAsync(fileId, ct) is not { } f) return "Filen findes ikke længere.";
        CollabNote n;
        try { n = CollabNote.Write(fileId, by, text, clock.GetUtcNow().UtcDateTime); }
        catch (ArgumentException e) { return e.Message; }
        await store.AddNoteAsync(n, ct);
        await TellMartinAsync(by, $"{by} skrev om \"{f.Title}\" på /{f.Space}/{f.Room}:\n{n.Text}", ct);
        return null;
    }

    /// <summary>The file and where it lies on disk - only when it belongs to this space.</summary>
    public async Task<(CollabFile File, string Path)?> OpenAsync(int id, string space, CancellationToken ct = default) =>
        await store.FileAsync(id, ct) is { } f && f.Space == space && disk.Find(f.Space, f.StoredName) is { } path ? (f, path) : null;

    public async Task<CollabFile?> FileAsync(int id, CancellationToken ct = default) => await store.FileAsync(id, ct);

    /// <summary>Martin only (checked by the endpoint): the file, its thoughts and the file on disk.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (await store.FileAsync(id, ct) is not { } f) return;
        await store.DeleteFileAsync(id, ct);
        disk.Delete(f.Space, f.StoredName);
    }

    // Martin's own uploads and thoughts do not ring his own bell. A bell that fails never stops the save.
    private async Task TellMartinAsync(string by, string text, CancellationToken ct)
    {
        if (by == CollabRules.Owner) return;
        try { await inbox.NewRequestAsync(by, "", text, ct); }
        catch (Exception) { }
    }
}
