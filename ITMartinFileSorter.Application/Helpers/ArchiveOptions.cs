namespace ITMartinFileSorter.Application.Helpers;

public class ArchiveOptions
{
    public bool UseYearFolders { get; set; } = true;

    public bool UseMonthFolders { get; set; } = true;

    public bool UseTypeFolders { get; set; } = false;

    public bool UseTripFolders { get; set; } = true;

    public bool RenameFiles { get; set; } = true;

    public RenameStrategy RenameStrategy { get; set; }
        = RenameStrategy.AlbumStyle;
}