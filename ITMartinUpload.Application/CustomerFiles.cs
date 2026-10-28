using ITMartinUpload.Domain;

namespace ITMartinUpload.Application;

public enum ReceiveResult { Saved, AlreadyHere, Unauthorized, BadSlug, BadFileName }

/// <summary>A file the customer may open: the path on disk and how to send it.</summary>
public sealed record OpenedFile(string Path, string ContentType, byte[]? Thumbnail);

/// <summary>
/// What the customer sends and sees: receiving a file (with one ping to Martin per burst), the list of what they
/// sent, taking a file back out, and FileSorter's overview of what happened to each file.
/// </summary>
public sealed class CustomerFiles(CustomerBook customers, IUploadFiles files, ISortReports sortReports,
    IPushSender push, IThumbnails thumbnails, UploadActivity activity)
{
    public async Task<ReceiveResult> ReceiveAsync(string slug, string? token, string? fileName, string? folder,
        long size, Stream content, CancellationToken ct = default)
    {
        if (!customers.IsAuthorized(slug, token)) return ReceiveResult.Unauthorized;
        if (!UploadNames.IsValidSlug(slug)) return ReceiveResult.BadSlug;
        var safeName = UploadNames.SafeFileName(fileName);
        if (safeName is null) return ReceiveResult.BadFileName;

        // Music customers keep the Artist/Album folder Rip CD sends along ("path" form field).
        var customer = customers.Find(slug);
        var subfolder = customer?.Music == true ? UploadNames.SafeSubfolder(folder) : null;
        if (customer?.Music == true && files.AlreadyHaveIn(slug, subfolder, safeName, size)) return ReceiveResult.AlreadyHere;

        await files.SaveAsync(slug, safeName, content, ct, subfolder);

        // One ping per batch, not one per photo: a customer sending 400 pictures
        // must not put 400 notifications on Martin's phone.
        if (activity.ShouldAnnounceUpload(slug))
        {
            var name = customer?.Name ?? slug;
            _ = push.SendAsync(IPushSender.Martin,
                customer?.Music == true ? $"{name} sender musik" : $"{name} sender billeder",
                "Filerne lander i deres mappe på NAS'en.",
                "/svar");
        }
        return ReceiveResult.Saved;
    }

    /// <summary>A photo taken at an event: saved as "Martin - 20260925-143012-1.jpg" so the feed shows who took it.</summary>
    public async Task<bool> SaveEventPhotoAsync(string slug, string by, int number, string extension, Stream content)
    {
        var name = UploadNames.SafeFileName($"{by} - {DateTime.Now:yyyyMMdd-HHmmss}-{number}{extension}");
        if (name is null) return false;
        await files.SaveAsync(slug, name, content);
        return true;
    }

    /// <summary>Files the browser should not send again: same name and size already here (same folder for music).</summary>
    public bool AlreadyHave(UploadCustomer? customer, string slug, string fileName, string? folder, long size) =>
        customer?.Music == true
            ? UploadNames.SafeFileName(fileName) is { } safe && files.AlreadyHaveIn(slug, UploadNames.SafeSubfolder(folder), safe, size)
            : files.AlreadyHave(slug, fileName, size);

    public IReadOnlyList<StoredFile> List(string slug) => files.List(slug);

    public IReadOnlyList<StoredAlbum> Albums(string slug) => files.Albums(slug);

    public bool Remove(string slug, string? token, string fileName) =>
        customers.IsAuthorized(slug, token) && files.Remove(slug, fileName);

    /// <summary>One of the customer's own files, or a small preview of it; null when the link is wrong or the file is not theirs.</summary>
    public OpenedFile? Open(string slug, string? token, string fileName, bool thumb)
    {
        if (!customers.IsAuthorized(slug, token)) return null;
        var path = files.ResolveOwnFile(slug, fileName);
        if (path is null) return null;
        var preview = thumb && UploadNames.IsImage(path) ? thumbnails.Create(path) : null;
        return new OpenedFile(path, UploadNames.ContentTypeFor(path), preview);
    }

    /// <summary>FileSorter's _kilder.json paired with the uploaded files. A missing report means "nothing sorted yet".</summary>
    public (IReadOnlyList<FileSortStatus> Files, SortSummary Summary) SortStatus(UploadCustomer customer)
    {
        var report = UploadNames.IsValidSlug(customer.LibraryName) ? sortReports.Read(customer.LibraryName) : null;
        var sorted = SortOverview.Build(files.List(customer.Slug).Select(f => new UploadedFile(f.Name, f.ModifiedUtc)), report);
        return (sorted, SortOverview.Summarize(sorted, report));
    }
}
