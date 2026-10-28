using ITMartinUpload.Domain;

namespace ITMartinUpload.Application;

// A customer's folder on the NAS (disk in Infrastructure). Every method stays inside the customer's own folder.
public interface IUploadFiles
{
    /// <summary>Saves under the first free name (never overwrites); returns the full path.</summary>
    Task<string> SaveAsync(string slug, string safeName, Stream content, CancellationToken ct = default, string? subfolder = null);
    IReadOnlyList<StoredFile> List(string slug);
    IReadOnlyList<StoredAlbum> Albums(string slug);
    bool AlreadyHave(string slug, string fileName, long size);
    bool AlreadyHaveIn(string slug, string? subfolder, string safeName, long size);
    /// <summary>The full path of one of the customer's own files, or null - never a path outside the folder.</summary>
    string? ResolveOwnFile(string slug, string fileName);
    /// <summary>Moves the file to the trash folder; nothing is deleted.</summary>
    bool Remove(string slug, string fileName);
}

// The conversation with one customer (a json file next to their uploads).
public interface IMessageStore
{
    IReadOnlyList<ChatMessage> Read(string slug);
    void Append(string slug, ChatMessage message);
}

// FileSorter's _kilder.json in the customer's library (read-only). Null = nothing sorted yet.
public interface ISortReports
{
    SortReport? Read(string library);
}

public sealed record PushRegistration(string Endpoint, string P256DH, string Auth, string Who);

// Web push to Martin ("martin") or a customer (their slug).
public interface IPushSender
{
    public const string Martin = "martin";

    string PublicKey { get; }
    void Subscribe(string who, PushRegistration registration);
    /// <summary>Never throws - a push that does not arrive must not take an upload or a message down with it.</summary>
    Task SendAsync(string who, string title, string body, string? url = null);
}

// Small previews of photos (ImageSharp in Infrastructure). Null = show the generic icon.
public interface IThumbnails
{
    byte[]? Create(string path);
}
