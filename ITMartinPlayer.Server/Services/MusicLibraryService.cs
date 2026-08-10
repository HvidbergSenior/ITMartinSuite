using System.Text.RegularExpressions;

namespace ITMartinPlayer.Server.Services;

public sealed record Track(string RelativePath, int? TrackNumber, string Title, string Artist, string Album, TimeSpan Duration);
public sealed record Album(string Artist, string Name, string FolderRelativePath, string? CoverRelativePath, List<Track> Tracks);

// Rippers don't agree on one layout. dbPoweramp (and most) drop one folder
// per CD as "Artist - Album" directly under the library root. But plenty of
// older/manually-organized artist folders instead hold one subfolder per
// album (sometimes several levels for box sets/multi-disc), with the actual
// tracks only appearing at whatever folder is the leaf. So rather than
// assume a fixed depth, every folder that directly contains audio files
// becomes its own "album" - a folder one level under the root names itself
// ("Artist - Album"); anything deeper borrows its artist from the top-level
// ancestor folder and uses its own name as the album title.
public sealed class MusicLibraryService
{
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac", ".wma"];
    private static readonly Regex TrackNamePattern = new(@"^(\d{1,3})[\s._-]+(.+)$", RegexOptions.Compiled);

    // Folder names ripped from CDs often store "Band, The" instead of
    // "The Band" (old alphabetization convention), which splits one band
    // into two separate artist entries. Normalize on the way in so grouping
    // and display both land on the same name.
    private static readonly Regex TrailingArticlePattern = new(@"^(.+),\s*(The|A|An)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string NormalizeArtist(string artist)
    {
        var match = TrailingArticlePattern.Match(artist.Trim());
        return match.Success ? $"{match.Groups[2].Value} {match.Groups[1].Value}" : artist;
    }

    // Priority order for folder-level cover art - checked before falling
    // back to a track's embedded ID3 picture (which needs a per-request
    // decode, so it's worth avoiding when a plain image file already sits
    // right there, as it does for most of this library).
    private static readonly string[] CoverFileNames =
        ["folder.jpg", "cover.jpg", "front.jpg", "albumartsmall.jpg"];

    public string Root { get; }

    // A personal library on a slow network share, re-walked plus TagLib-read
    // per file, is too slow to redo on every single page render - cache the
    // whole scan for the process lifetime and let a restart pick up changes.
    private readonly Lazy<List<Album>> _albums;

    public MusicLibraryService(IConfiguration config)
    {
        Root = config["PlayerSettings:LibraryRoot"] ?? "/music-library";
        _albums = new Lazy<List<Album>>(ScanAlbums);
    }

    public List<Album> GetAlbums() => _albums.Value;

    public List<Track> GetAllTracks() => GetAlbums().SelectMany(a => a.Tracks).ToList();

    public List<(string Artist, List<Album> Albums)> GetArtists() =>
        GetAlbums()
            .GroupBy(a => a.Artist, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Artist: g.Key, Albums: g.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();

    private List<Album> ScanAlbums()
    {
        var albums = new List<Album>();
        if (!Directory.Exists(Root)) return albums;

        foreach (var topFolder in Directory.EnumerateDirectories(Root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var topName = Path.GetFileName(topFolder);
            if (topName.StartsWith('.') || topName.StartsWith('#')) continue;

            foreach (var leaf in EnumerateAlbumFolders(topFolder))
            {
                var album = BuildAlbum(leaf, topFolder, topName);
                if (album is not null) albums.Add(album);
            }
        }

        return albums.OrderBy(a => a.Artist, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // A folder "counts" as an album folder if it directly holds audio files
    // itself. Folders that only contain subfolders (a pure artist bucket)
    // are walked into but never listed as an album in their own right.
    private static IEnumerable<string> EnumerateAlbumFolders(string folder)
    {
        if (HasAudioFiles(folder))
            yield return folder;

        foreach (var sub in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(sub);
            if (name.StartsWith('.') || name.StartsWith('#')) continue;

            foreach (var deeper in EnumerateAlbumFolders(sub))
                yield return deeper;
        }
    }

    private static bool HasAudioFiles(string folder) =>
        Directory.EnumerateFiles(folder).Any(f => AudioExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

    private Album? BuildAlbum(string leaf, string topFolder, string topName)
    {
        var (rawArtist, albumName) = IsSameDir(leaf, topFolder)
            ? SplitFolderName(topName)
            : (topName, Path.GetFileName(leaf));
        var artist = NormalizeArtist(rawArtist);

        var tracks = new List<Track>();
        // Embedded ID3 tags beat folder-name guessing when present - a folder
        // like "Anna David 2005" has no " - " separator for SplitFolderName
        // to work with, so without this every differently-named folder for
        // the same artist becomes its own fake "artist" instead of grouping
        // under one. Real tags don't have that ambiguity. Read from the first
        // few tracks (not just one) since a single mistagged file shouldn't
        // override an otherwise-consistent album.
        var tagArtistVotes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagAlbumVotes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(leaf))
        {
            var ext = Path.GetExtension(file);
            if (!AudioExt.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

            var name = Path.GetFileNameWithoutExtension(file);
            var match = TrackNamePattern.Match(name);
            var (trackNum, title) = match.Success
                ? (int.Parse(match.Groups[1].Value), match.Groups[2].Value)
                : ((int?)null, name);

            var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
            var (duration, tagArtist, tagAlbum) = ReadTags(file);

            if (!string.IsNullOrWhiteSpace(tagArtist))
                tagArtistVotes[tagArtist] = tagArtistVotes.GetValueOrDefault(tagArtist) + 1;
            if (!string.IsNullOrWhiteSpace(tagAlbum))
                tagAlbumVotes[tagAlbum] = tagAlbumVotes.GetValueOrDefault(tagAlbum) + 1;

            // Artist/Album get corrected below once tag votes are in - Track
            // is a record, so this list is patched in a second pass rather
            // than re-reading every file.
            tracks.Add(new Track(rel, trackNum, title, artist, albumName, duration));
        }

        if (tracks.Count == 0) return null;

        var bestTagArtist = tagArtistVotes.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        var bestTagAlbum = tagAlbumVotes.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        if (!string.IsNullOrWhiteSpace(bestTagArtist)) artist = NormalizeArtist(bestTagArtist);
        if (!string.IsNullOrWhiteSpace(bestTagAlbum)) albumName = bestTagAlbum;

        tracks = tracks
            .Select(t => t with { Artist = artist, Album = albumName })
            .OrderBy(t => t.TrackNumber ?? int.MaxValue).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
        var cover = FindFolderCover(leaf);
        return new Album(artist, albumName, Path.GetRelativePath(Root, leaf).Replace('\\', '/'), cover, tracks);
    }

    private static (TimeSpan Duration, string? Artist, string? Album) ReadTags(string file)
    {
        try
        {
            using var tagFile = TagLib.File.Create(file);
            var duration = tagFile.Properties?.Duration ?? TimeSpan.Zero;
            var tagArtist = tagFile.Tag?.FirstAlbumArtist ?? tagFile.Tag?.FirstPerformer;
            var tagAlbum = tagFile.Tag?.Album;
            return (duration, tagArtist, tagAlbum);
        }
        catch
        {
            return (TimeSpan.Zero, null, null);
        }
    }

    private string? FindFolderCover(string folder)
    {
        var files = Directory.EnumerateFiles(folder)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0) return null;

        foreach (var wanted in CoverFileNames)
        {
            var match = files.FirstOrDefault(f => Path.GetFileName(f).Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return Path.GetRelativePath(Root, match).Replace('\\', '/');
        }

        // "AlbumArt_{guid}_Large.jpg" over "..._Small.jpg", otherwise just
        // whatever image is there - better than nothing.
        var large = files.FirstOrDefault(f => Path.GetFileName(f).Contains("large", StringComparison.OrdinalIgnoreCase));
        var chosen = large ?? files[0];
        return Path.GetRelativePath(Root, chosen).Replace('\\', '/');
    }

    private static bool IsSameDir(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static (string Artist, string Album) SplitFolderName(string folderName)
    {
        var parts = folderName.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (folderName, "");
    }
}
