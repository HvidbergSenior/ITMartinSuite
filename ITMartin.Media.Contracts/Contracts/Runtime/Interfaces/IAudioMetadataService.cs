namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

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

    int? GetTrackNumber(
        string path);

    // Embedded album art (ID3 APIC frame / MP4 covr atom), if present.
    byte[]? GetCoverArt(
        string path);
}