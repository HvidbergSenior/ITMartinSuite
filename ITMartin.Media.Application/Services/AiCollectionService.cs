using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Services;

public class AiCollectionService
    : IAiCollectionService
{
    public List<AiCollection> BuildCollections(
        List<MediaFile> files)
    {
        var collections =
            new List<AiCollection>();

        // =========================
        // CATEGORY COLLECTIONS
        // =========================

        var categoryGroups =
            files
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.AiCategory))
                .GroupBy(x => x.AiCategory);

        foreach (var group in categoryGroups)
        {
            collections.Add(
                new AiCollection
                {
                    Title = group.Key!,
                    Description =
                        $"{group.Count()} files",

                    Category = group.Key!,

                    Files = group.ToList()
                });
        }

        // =========================
        // SUBCATEGORY COLLECTIONS
        // =========================

        var subGroups =
            files
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.AiSubCategory))
                .GroupBy(x => x.AiSubCategory);

        foreach (var group in subGroups)
        {
            if (group.Count() < 3)
                continue;

            collections.Add(
                new AiCollection
                {
                    Title = group.Key!,
                    Description =
                        $"Smart collection: {group.Key}",

                    Category = "Smart",

                    Files = group.ToList()
                });
        }

        return collections
            .OrderByDescending(x => x.Files.Count)
            .ToList();
    }
}