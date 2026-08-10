
using System.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Metadata;

public sealed class AudioMetadataService
    : IAudioMetadataService
{
    public TimeSpan? GetDuration(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            return file.Properties.Duration;
        }
        catch
        {
            return null;
        }
    }

    public string? GetArtist(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            return file.Tag.FirstPerformer;
        }
        catch
        {
            return null;
        }
    }

    public string? GetAlbum(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            return file.Tag.Album;
        }
        catch
        {
            return null;
        }
    }

    public string? GetTitle(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            return file.Tag.Title;
        }
        catch
        {
            return null;
        }
    }

    public int? GetYear(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            return (int)file.Tag.Year;
        }
        catch
        {
            return null;
        }
    }

    public int? GetTrackNumber(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            var track = (int)file.Tag.Track;

            return track > 0 ? track : null;
        }
        catch
        {
            return null;
        }
    }

    public byte[]? GetCoverArt(
        string path)
    {
        try
        {
            using var file =
                TagLib.File.Create(path);

            var picture =
                file.Tag.Pictures.FirstOrDefault();

            return picture?.Data?.Data;
        }
        catch
        {
            return null;
        }
    }
}