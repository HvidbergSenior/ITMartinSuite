using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class DuplicateService
{
    public string FolderPath { get; set; } = "";

    public List<MediaFile> AllFiles { get; set; } = new();

    // ❌ No longer used — kept empty to avoid breaking anything
    public List<List<MediaFile>> DuplicateGroups => new();

    public bool DuplicatesHandled { get; set; } = false;

    public bool IsProcessing { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }

    public HashSet<MediaMainCategory> CompletedCategories { get; set; } = new();

    public GroupingOptions? GroupingOptions { get; set; }

    public event Action? OnChange;
    public void NotifyStateChanged() => OnChange?.Invoke();

    // ===== EXPORT =====
    public IEnumerable<MediaFile> FilesToExport =>
        AllFiles.Where(f =>
            f.Status == MediaFileStatus.ToKeep ||
            f.SubCategory == MediaSubCategory.Meme ||
            f.SubCategory == MediaSubCategory.Screenshot);

    // ===== BUILD DUPLICATES (AUTO HANDLE ONLY) =====
    public void BuildDuplicateGroups()
    {
        var grouped = AllFiles
            .GroupBy(f => f.Hash)
            .Where(g => g.Count() > 1);

        foreach (var group in grouped)
        {
            var list = group.ToList();

            // 🔥 Always auto-handle duplicates
            AutoHandleGroup(list);
        }

        DuplicatesHandled = true;

        NotifyStateChanged();
    }

    // ===== RESET =====
    public void Reset()
    {
        AllFiles.Clear();
        CompletedCategories.Clear();
        DuplicatesHandled = false;
        GroupingOptions = null;
    }

    // ===== AUTO HANDLE (CORE LOGIC) =====
    private void AutoHandleGroup(List<MediaFile> group)
    {
        // keep newest file
        var keep = group
            .OrderByDescending(f => f.CreatedAt ?? DateTime.MinValue)
            .First();

        foreach (var file in group)
        {
            file.Status = file == keep
                ? MediaFileStatus.ToKeep
                : MediaFileStatus.ToDelete;

            file.RequiresReview = false;
        }
    }

    // ===== OPTIONAL (still usable if referenced elsewhere) =====
    public bool IsGroupHandled(List<MediaFile> group)
    {
        return group.Count(f => f.Status == MediaFileStatus.ToDelete) == group.Count - 1;
    }
}