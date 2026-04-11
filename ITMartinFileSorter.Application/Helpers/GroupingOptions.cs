namespace ITMartinFileSorter.Application.Helpers;

public class GroupingOptions
{
    public GroupingStrategy Strategy { get; set; } = GroupingStrategy.None;

    public string? CustomPrefix { get; set; }

    public bool CreateSubfolders { get; set; } = true;

    public bool RenameFiles { get; set; } = true;
}