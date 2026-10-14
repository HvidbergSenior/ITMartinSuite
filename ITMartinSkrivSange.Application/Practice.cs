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
}
