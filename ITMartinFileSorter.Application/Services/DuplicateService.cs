using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class DuplicateService
{
    public string FolderPath { get; set; } = "";

    public List<MediaFile> AllFiles { get; set; } = new();

    public void Reset()
    {
        AllFiles.Clear();
    }

    public void BuildDuplicateGroups()
    {
        if (AllFiles.Any(f => f.Hash == null))
            throw new InvalidOperationException("Hash must be set before duplicate detection.");

        var groups = AllFiles
            .GroupBy(f => f.Hash)
            .Where(g => g.Key != null && g.Count() > 1);

        foreach (var group in groups)
        {
            HandleGroup(group.ToList());
        }
    }

    private void HandleGroup(List<MediaFile> group)
    {
        var keep = SelectBestFile(group);
        var isSafe = IsSafeDuplicate(group);

        foreach (var file in group)
        {
            if (file.Status != MediaFileStatus.Initial)
                continue;

            if (!isSafe)
            {
                file.Status = MediaFileStatus.ToKeep;
                file.RequiresReview = true;
                continue;
            }

            if (file == keep)
            {
                file.Status = MediaFileStatus.ToKeep;
                file.RequiresReview = false;
            }
            else
            {
                file.Status = MediaFileStatus.ToDelete;
                file.RequiresReview = false;
            }
        }
    }

    private MediaFile SelectBestFile(List<MediaFile> group)
    {
        return group
            .OrderByDescending(f => f.IsDateReliable)
            .ThenByDescending(f => f.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(f => f.SizeBytes)
            .ThenBy(f => f.FullPath)
            .First();
    }

    private bool IsSafeDuplicate(List<MediaFile> group)
    {
        var sizes = group.Select(f => f.SizeBytes).Distinct().Count();
        var names = group.Select(f => f.FileName).Distinct().Count();

        return sizes == 1 && names == 1;
    }
}