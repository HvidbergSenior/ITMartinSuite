namespace ITMartin.Media.Application.Models;

public class Package1ExportResult
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