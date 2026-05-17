using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Models;

public class Package1ScanResult
{
    public List<MediaFile> Files
    {
        get;
        set;
    } = [];

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

    public int DuplicateGroups
    {
        get;
        set;
    }

    public long TotalBytes
    {
        get;
        set;
    }

    public long BytesToDelete
    {
        get;
        set;
    }

    public long BytesToKeep
    {
        get;
        set;
    }

    // ====================================
    // CLEANUP
    // ====================================

    public Package1CleanupResult Cleanup
    {
        get;
        set;
    } = new();
}