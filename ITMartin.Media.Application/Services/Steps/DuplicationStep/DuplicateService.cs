using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Services.Steps.DuplicationStep;

public sealed class DuplicateService
    : IDuplicateService
{
    public List<DuplicateGroup> BuildDuplicateGroups(
        IReadOnlyCollection<MediaFile> files)
    {
        return files
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x.Hash))
            .GroupBy(x => x.Hash)
            .Where(x => x.Count() > 1)
            .Select(group =>
                new DuplicateGroup
                {
                    Hash = group.Key!,
                    Files = group.ToList()
                })
            .ToList();
    }
}