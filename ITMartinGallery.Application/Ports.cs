using System.Text.Json.Nodes;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Application;

// The libraries on disk (read-only mounts on the NAS). Paths are absolute.
public interface ILibraryDisk
{
    bool DirectoryExists(string dir);
    bool FileExists(string file);
    IEnumerable<string> Directories(string dir);
    IEnumerable<string> Files(string dir, string pattern = "*");
    IEnumerable<string> AllFiles(string root, string pattern = "*");
    DateTime LastWriteUtc(string file);
    long Length(string file);
    string? ReadText(string file);
    string Md5(string file);
    /// <summary>For a symlinked file: the final target (may be missing). Null = not a link (or no such file).</summary>
    string? LinkTarget(string file);
}

// QuickSort's manifest.json. Null = no manifest for this library.
public interface IManifests
{
    Task<IReadOnlyList<ManifestEntry>?> LoadAsync(string libraryPath, CancellationToken ct);
}

// collections.json (the "Samlinger" cards) and the captions.json sidecars AI-billedtekster writes.
public interface ICollections
{
    IReadOnlyList<Collection> Load(string libraryPath);
    /// <summary>fileName -> caption for one folder; empty when there are none.</summary>
    Dictionary<string, string> Captions(string dir);
}

// The Musik Studio database (read-only). Empty when it is not mounted.
public interface ISongSheetSource
{
    IReadOnlyList<SongRow> Songs();
}

// Cover art embedded in a music file's ID3/MP4 tag.
public interface IEmbeddedCovers
{
    (byte[] Data, string MimeType)? Read(string file);
}

// EXIF/QuickTime capture date of a picture or video.
public interface ICaptureDates
{
    DateTime? Read(string file);
}

// The edits made on a birthday page (DataDir/celebrations/<slug>.json, writable - the library itself is read-only).
public interface ICelebrationEdits
{
    JsonObject? Read(string slug);
    void Write(string slug, JsonObject edits);
}
