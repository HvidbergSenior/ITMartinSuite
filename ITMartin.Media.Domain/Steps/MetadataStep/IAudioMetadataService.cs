namespace ITMartin.Media.Domain.Steps.MetadataStep;

public interface IAudioMetadataService
{
    TimeSpan? GetDuration(
        string path);

    string? GetArtist(
        string path);

    string? GetAlbum(
        string path);

    string? GetTitle(
        string path);

    int? GetYear(
        string path);
}