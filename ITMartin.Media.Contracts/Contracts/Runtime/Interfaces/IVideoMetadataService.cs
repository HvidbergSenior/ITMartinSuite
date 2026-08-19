namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoMetadataService
{
    DateTime? GetCreationTime(string path);

    // Null if ffprobe couldn't read a video stream at all (corrupt/unreadable
    // file) - callers should treat that the same as "needs a closer look",
    // not "this is fine".
    string? GetVideoCodec(string path);

    string GetModelFromFileName(string path);
    TimeSpan? GetDuration(
        string path);

    (int Width, int Height)? GetDimensions(
        string path);
}