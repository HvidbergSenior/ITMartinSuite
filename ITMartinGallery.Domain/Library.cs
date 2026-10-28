namespace ITMartinGallery.Domain;

// One file from QuickSort's manifest.json - what "På denne dag" and search need of it.
public sealed record ManifestEntry(DateTime? CreatedAt, string? ExportedPath, IReadOnlyList<string> AiTags);

// One "Samlinger" card from collections.json; FilePaths are already absolute.
public sealed record Collection(string Name, string Type, IReadOnlyList<string> FilePaths);

// One song row from the Musik Studio database (studio.db).
public sealed record SongRow(string Title, string SourceFile, string Key, int? Tempo, string Chart, string Lyrics, string Notes, string Strum, string Fingerpick);

// The Sange gallery is where he practises away from the PC, so a song's chart and lyrics from studio.db ride
// along with the audio. Matched on folder/file of the studio's SourceFile, falling back to any song in the same
// folder so every take of a song shows the same sheet.
public static class SongMatch
{
    // 3 = same folder and file, 2 = same file, 1 = same folder (or the song is named like the folder), 0 = no match.
    public static int Score(string sourceFile, string title, string file, string folder)
    {
        var src = sourceFile.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sFile = src.Length > 0 ? src[^1] : "";
        var sFolder = src.Length > 1 ? src[^2] : "";
        return string.Equals(sFile, file, StringComparison.OrdinalIgnoreCase) && string.Equals(sFolder, folder, StringComparison.OrdinalIgnoreCase) ? 3
             : string.Equals(sFile, file, StringComparison.OrdinalIgnoreCase) ? 2
             : folder.Length > 0 && (string.Equals(sFolder, folder, StringComparison.OrdinalIgnoreCase) || string.Equals(title, folder, StringComparison.OrdinalIgnoreCase)) ? 1
             : 0;
    }
}

// "Lidt navne rundtomkring" (theme "bedstemor"): person folder names shown the way the family writes them.
public static class PeopleNames
{
    public const string PeopleRel = "SmartFolders/People";

    // Folder names are ASCII-safe; show them the way the family writes them.
    private static readonly Dictionary<string, string> Display = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Joergen"] = "Jørgen",
    };

    public static string DisplayName(string folder) => Display.GetValueOrDefault(folder, folder);

    public static bool WithPeople(Gallery g) => string.Equals(g.Theme, "bedstemor", StringComparison.OrdinalIgnoreCase);
}
