namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class LibraryPolishResult
{
    public int EmptyFoldersRemoved { get; init; }
    public int JunkFilesRemoved { get; init; }
    public int ManifestsHidden { get; init; }
    public int MisclassifiedScreenshotsFixed { get; init; }
    public int UnplayableVideosQuarantined { get; init; }
}
