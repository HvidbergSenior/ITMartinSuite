using ITMartinFileSorter.Application.Helpers; // 🔥 REQUIRED
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class DuplicateService
{
    public string FolderPath { get; set; } = "";

    public List<MediaFile> AllFiles { get; set; } = new();

    public List<List<MediaFile>> DuplicateGroups { get; set; } = new();

    public bool DuplicatesHandled { get; set; } = false;

    public bool IsProcessing { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }

    public HashSet<MediaMainCategory> CompletedCategories { get; set; } = new();

    // ✅ ADD THIS BACK
    public GroupingOptions? GroupingOptions { get; set; }

    public event Action? OnChange;
    public void NotifyStateChanged() => OnChange?.Invoke();

    // ===== EXPORT =====
    public IEnumerable<MediaFile> FilesToExport =>
        AllFiles.Where(f =>
            f.Status == MediaFileStatus.ToKeep ||
            f.SubCategory == MediaSubCategory.Meme ||
            f.SubCategory == MediaSubCategory.Screenshot);

    // ===== BUILD DUPLICATES =====
    public void BuildDuplicateGroups()
    {
        DuplicateGroups.Clear();

        var grouped = AllFiles
            .GroupBy(f => f.Hash)
            .Where(g => g.Count() > 1);

        foreach (var group in grouped)
        {
            var list = group.ToList();

            // ✅ AUTO HANDLE LOW VALUE FILES
            if (list.All(f =>
                    f.SubCategory == MediaSubCategory.Meme ||
                    f.SubCategory == MediaSubCategory.Screenshot))
            {
                AutoHandleLowValueGroup(list);
                continue;
            }

            DuplicateGroups.Add(list);
        }

        NotifyStateChanged();
    }

    // ===== RESET =====
    public void Reset()
    {
        AllFiles.Clear();
        DuplicateGroups.Clear();
        CompletedCategories.Clear();
        DuplicatesHandled = false;

        // ✅ ALSO RESET GROUPING
        GroupingOptions = null;
    }

    // ===== AUTO HANDLE =====
    private void AutoHandleLowValueGroup(List<MediaFile> group)
    {
        var newest = group
            .OrderByDescending(f => f.CreatedAt ?? DateTime.MinValue)
            .First();

        foreach (var file in group)
        {
            file.Status = file == newest
                ? MediaFileStatus.ToKeep
                : MediaFileStatus.ToDelete;

            file.RequiresReview = false;
        }
    }

    // ===== HELPERS =====
    public bool IsGroupHandled(List<MediaFile> group)
    {
        return group.Count(f => f.Status == MediaFileStatus.ToDelete) == group.Count - 1;
    }
}