namespace ITMartinFileSorter.Domain.Entities;

public class FolderItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int FileCount { get; set; }
}