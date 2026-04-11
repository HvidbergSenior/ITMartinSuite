using ITMartinFileSorter.Application.Helpers;
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

    public GroupingOptions? GroupingOptions { get; set; }

    public event Action? OnChange;
    public void NotifyStateChanged() => OnChange?.Invoke();

    public IEnumerable<MediaFile> FilesToExport =>
        AllFiles.Where(f => f.Status == MediaFileStatus.ToKeep);

    // ===== PAGING =====

    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;

    public int TotalPages =>
        (int)Math.Ceiling((double)DuplicateGroups.Count / PageSize);

    public IEnumerable<List<MediaFile>> CurrentPageGroups =>
        DuplicateGroups
            .Skip(CurrentPage * PageSize)
            .Take(PageSize);

    // ===== BUILD DUPLICATES =====

    public void BuildDuplicateGroups()
    {
        DuplicateGroups = AllFiles
            .GroupBy(f => f.Hash)
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList())
            .OrderByDescending(g => g.Count)
            .ToList();

        CurrentPage = 0;

        NotifyStateChanged();
    }

    // ===== RESET =====

    public void Reset()
    {
        AllFiles.Clear();
        DuplicateGroups.Clear();
        CompletedCategories.Clear();
        GroupingOptions = null;
        DuplicatesHandled = false;
        CurrentPage = 0;
    }

    // ===== CANCELLATION =====

    public CancellationTokenSource? Cancellation { get; set; }

    public void Cancel()
    {
        Cancellation?.Cancel();
    }
}