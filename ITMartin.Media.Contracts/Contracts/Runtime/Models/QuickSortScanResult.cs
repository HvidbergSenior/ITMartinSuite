namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public class QuickSortScanResult
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

    public QuickSortCleanupResult Cleanup
    {
        get;
        set;
    } = new();
}