namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public class QuickSortExportResult
{
    public int ExportedFiles
    {
        get;
        set;
    }

    public int SkippedFiles
    {
        get;
        set;
    }

    public long ExportedBytes
    {
        get;
        set;
    }

    public string ExportRoot
    {
        get;
        set;
    } = "";

    public TimeSpan Duration
    {
        get;
        set;
    }

    public bool Success
    {
        get;
        set;
    }

    public string? ErrorMessage
    {
        get;
        set;
    }
}