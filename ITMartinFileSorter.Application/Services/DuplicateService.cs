using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Enums;
using ITMartinFileSorter.Application.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class DuplicateService : IDuplicateService
{
    public string FolderPath { get; set; } = "";

    public List<MediaFile> AllFiles { get; set; } = [];

    // =========================
    // AI COLLECTIONS
    // =========================

    public List<AiCollection> AiCollections { get; set; } = [];

    public void Reset()
    {
        AllFiles.Clear();

        AiCollections.Clear();
    }

    public void BuildDuplicateGroups()
    {
        if (AllFiles.Any(f => f.Hash == null))
        {
            throw new InvalidOperationException(
                "Hash must be set before duplicate detection.");
        }

        var groups = AllFiles
            .GroupBy(f => f.Hash)
            .Where(g =>
                g.Key != null &&
                g.Count() > 1);

        foreach (var group in groups)
        {
            HandleGroup(group.ToList());
        }
    }

    private void HandleGroup(
        List<MediaFile> group)
    {
        var keep = SelectBestFile(group);

        var isSafe = IsSafeDuplicate(group);

        foreach (var file in group)
        {
            // =========================
            // NEVER TOUCH AUTO CATEGORIES
            // =========================

            if (file.SubCategory is
                MediaSubCategory.Meme or
                MediaSubCategory.Screenshot)
            {
                continue;
            }

            // =========================
            // MANUAL REVIEW
            // =========================

            if (!isSafe)
            {
                file.Status =
                    MediaFileStatus.Initial;

                file.RequiresReview = true;

                continue;
            }

            // =========================
            // AUTO RESOLVE
            // =========================

            if (file == keep)
            {
                file.Status =
                    MediaFileStatus.ToKeep;

                file.RequiresReview = false;
            }
            else
            {
                file.Status =
                    MediaFileStatus.ToDelete;

                file.RequiresReview = false;
            }
        }
    }

    private MediaFile SelectBestFile(
        List<MediaFile> group)
    {
        return group
            .OrderByDescending(f =>
                f.IsDateReliable)

            .ThenByDescending(f =>
                f.CreatedAt ??
                DateTime.MinValue)

            .ThenByDescending(f =>
                f.SizeBytes)

            .ThenBy(f =>
                f.FullPath)

            .First();
    }

    private bool IsSafeDuplicate(
        List<MediaFile> group)
    {
        var sizes = group
            .Select(f => f.SizeBytes)
            .Distinct()
            .Count();

        var names = group
            .Select(f => f.FileName)
            .Distinct()
            .Count();

        return sizes == 1 &&
               names == 1;
    }
}