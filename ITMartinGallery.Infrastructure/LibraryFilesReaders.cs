using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Contracts.Entities;
using ITMartinGallery.Application;
using ITMartinGallery.Domain;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace ITMartinGallery.Infrastructure;

// QuickSort's manifest.json via the shared loader (it also rebases Windows paths).
public sealed class ManifestReader : IManifests
{
    private readonly QuickSortManifestLoader _loader = new();

    public async Task<IReadOnlyList<ManifestEntry>?> LoadAsync(string libraryPath, CancellationToken ct)
    {
        var manifest = await _loader.LoadAsync(libraryPath, ct);
        return manifest.MediaFiles.Select(f => new ManifestEntry(f.CreatedAt, f.ExportedPath, f.AiTags)).ToList();
    }
}

public sealed class CollectionsFile : ICollections
{
    public IReadOnlyList<Collection> Load(string libraryPath)
    {
        var path = Path.Combine(libraryPath, "collections.json");
        if (!File.Exists(path)) return [];
        try
        {
            var collections = JsonSerializer.Deserialize<List<MediaCollection>>(File.ReadAllText(path)) ?? [];

            // FilePaths are stored relative to the library root (portable across
            // whichever machine actually serves the library) - resolve to
            // absolute here, once, so every caller just gets a usable path.
            // Path.IsPathRooted also stays true for pre-existing absolute-style
            // entries (older collections.json files written before this fix, or
            // manually rebased ones) - those pass through unchanged.
            return collections.Select(c => new Collection(c.Name, c.Type, c.FilePaths
                    // Defensive: some collections.json files were written by a
                    // Windows dev box with backslash separators, which this Linux
                    // container's Path.Combine treats as a literal filename
                    // character rather than a directory separator - normalize
                    // before combining, on top of the writer-side fix.
                    .Select(f => f.Replace('\\', '/'))
                    .Select(f => Path.IsPathRooted(f) ? f : Path.GetFullPath(Path.Combine(libraryPath, f)))
                    .ToList()))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // AI-billedtekster writes this sidecar next to a generated Yearbook folder's
    // files - fileName -> caption. Missing/malformed just means no captions yet.
    public Dictionary<string, string> Captions(string dir)
    {
        var path = Path.Combine(dir, "captions.json");
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}

// studio.db from Musik Studio, mounted read-only at StudioDb (compose).
public sealed class StudioSongSheets(GalleryOptions options) : ISongSheetSource
{
    public IReadOnlyList<SongRow> Songs()
    {
        if (!File.Exists(options.StudioDb)) return [];
        var songs = new List<SongRow>();
        try
        {
            using var con = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.StudioDb};Mode=ReadOnly");
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "select Title, SourceFile, MusicKey, Tempo, ChordChart, Lyrics, Notes, StrumPattern, FingerpickPattern from Songs where ChordChart <> '' or Lyrics <> ''";
            using var rd = cmd.ExecuteReader();
            string Text(int i) => rd.IsDBNull(i) ? "" : rd.GetString(i);
            while (rd.Read())
                songs.Add(new SongRow(rd.GetString(0), Text(1), Text(2), rd.IsDBNull(3) ? null : rd.GetInt32(3),
                    Text(4), Text(5), Text(6), Text(7), Text(8)));
            return songs;
        }
        catch
        {
            return [];
        }
    }
}

// Reads the cover the same way ITMartinPlayer does.
public sealed class TagLibCovers : IEmbeddedCovers
{
    public (byte[] Data, string MimeType)? Read(string file)
    {
        try
        {
            using var tagFile = TagLib.File.Create(file);
            var picture = tagFile.Tag?.Pictures?.FirstOrDefault();
            return picture is null ? null : (picture.Data.Data, picture.MimeType ?? "image/jpeg");
        }
        catch
        {
            return null;
        }
    }
}

public sealed class ExifCaptureDates : ICaptureDates
{
    public DateTime? Read(string path)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(path);

            var sub = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (sub is not null &&
                (sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var d) ||
                 sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out d)) &&
                CelebrationRules.Plausible(d))
                return d;

            var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 is not null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out d) && CelebrationRules.Plausible(d))
                return d;

            var qt = dirs.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
            if (qt is not null && qt.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out d) && CelebrationRules.Plausible(d))
                return d.ToLocalTime();
        }
        catch
        {
            // Unreadable metadata is common on old scans - fall back to the name.
        }
        return null;
    }
}

// DataDir/celebrations/<slug>.json - the only place the gallery writes.
public sealed class CelebrationEditsFile(GalleryOptions options) : ICelebrationEdits
{
    private string FileOf(string slug) => Path.Combine(options.DataDir, "celebrations", slug.ToLowerInvariant() + ".json");

    public JsonObject? Read(string slug)
    {
        var file = FileOf(slug);
        if (!File.Exists(file)) return null;
        try { return JsonNode.Parse(File.ReadAllText(file)) as JsonObject; }
        catch (JsonException) { return null; } // a typo must not take the page down
    }

    public void Write(string slug, JsonObject edits)
    {
        var file = FileOf(slug);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, edits.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
