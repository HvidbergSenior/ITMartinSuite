namespace ITMartinKaraoke.Server.Services;

public sealed record LibraryTrack(string RelativePath, string Title, string Artist);

// Scans a plain folder of ripped-CD audio files - rip-cd.ps1 (run on the
// Windows host, since Docker/WSL2 can't see an optical drive) drops files
// here as "Artist - Title.ext"; anything without that separator just uses
// the filename as the title with an empty artist.
public sealed class KaraokeLibraryService
{
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];

    public string Root { get; }

    public KaraokeLibraryService(IConfiguration config)
    {
        Root = config["KaraokeSettings:LibraryRoot"] ?? "/karaoke-library";
    }

    public List<LibraryTrack> GetTracks()
    {
        var results = new List<LibraryTrack>();
        if (!Directory.Exists(Root)) return results;

        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!AudioExt.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(file);
                var parts = name.Split(" - ", 2, StringSplitOptions.TrimEntries);
                var (artist, title) = parts.Length == 2 ? (parts[0], parts[1]) : ("", name);

                results.Add(new LibraryTrack(rel, title, artist));
            }
        }
        catch { }

        return results.OrderBy(t => t.Artist).ThenBy(t => t.Title).ToList();
    }
}
