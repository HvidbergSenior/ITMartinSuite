using ITMartinSkrivSange.Domain;

namespace ITMartinSkrivSange.Application;

// Practising a part and recording it (user 2026-10-14: "a way to practice, record it ... find the right progressions for
// each verse, intro etc"). The browser records the mic and uploads it to recordings/{song}/take-{part}-{time}.webm.
public sealed class Practice(IStudioLibrary library)
{
    public List<RecordingFile> Takes(Song song, PartKind kind) =>
        library.GetRecordings(song.RecordingKey).Where(r => r.Section == Parts.Slug(kind)).ToList();

    public int TakeCount(Song song) => library.GetRecordings(song.RecordingKey).Count;

    public bool DeleteTake(RecordingFile take) => library.Delete(take.RelativePath);

    /// <summary>"Læg i galleriet" (user 2026-10-25: "i pushed del... but it is not on galery"): the take is copied into
    /// the koncert gallery as the song's title - "Space Oddity (David Bowie).mp3", then "... 2.mp3" for the next one.
    /// The take itself stays. Returns the name it got, or null when the take is gone.</summary>
    public string? PutInGallery(Song song, RecordingFile take) =>
        library.CopyToGallery(take.RelativePath, GalleryName(song.Title, Path.GetExtension(take.RelativePath), library.InGallery));

    public static string GalleryName(string title, string extension, Func<string, bool> taken)
    {
        var name = new string(title.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c is not '/' and not '\\').ToArray()).Trim();
        if (name == "") name = "Sang";
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? name : $"{name} {n}";
            if (!taken(candidate + extension)) return candidate;
        }
    }
}
