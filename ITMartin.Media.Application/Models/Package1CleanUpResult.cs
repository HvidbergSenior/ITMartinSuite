using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Models;

public class Package1CleanupResult
{
    public int TotalFiles
    {
        get;
        set;
    }

    public int KeepCount
    {
        get;
        set;
    }

    public int DeleteCount
    {
        get;
        set;
    }

    public long TotalBytes
    {
        get;
        set;
    }

    public long BytesToKeep
    {
        get;
        set;
    }

    public long BytesToDelete
    {
        get;
        set;
    }

    public List<MediaFile> KeepFiles
    {
        get;
        set;
    } = [];

    public List<MediaFile> DeleteFiles
    {
        get;
        set;
    } = [];
}