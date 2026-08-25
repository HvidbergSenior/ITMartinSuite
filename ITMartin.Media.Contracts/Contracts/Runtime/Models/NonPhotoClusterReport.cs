namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class SuspectFolder
{
    public string FolderPath { get; init; } = "";
    public int FileCount { get; init; }
    public int NoExifCount { get; init; }
    public long AvgFileSizeBytes { get; init; }
    public List<string> SampleFileNames { get; init; } = [];
}

// Report-only, same convention as DetectRotatedImagesAsync - nothing on disk
// changes. Meant to surface whole folders worth a 5-second human glance
// (thumbnails render fast in Package3 Studio) instead of either blind
// auto-move or paging through every undated photo one at a time.
public sealed class NonPhotoClusterReport
{
    public int FoldersScanned { get; init; }
    public List<SuspectFolder> SuspectFolders { get; init; } = [];
}
