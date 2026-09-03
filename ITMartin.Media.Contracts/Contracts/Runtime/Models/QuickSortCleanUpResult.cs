namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public class QuickSortCleanupResult
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