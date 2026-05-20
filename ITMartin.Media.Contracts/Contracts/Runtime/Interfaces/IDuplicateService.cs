using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IDuplicateService
{
        List<DuplicateGroup> BuildDuplicateGroups(
            IReadOnlyCollection<MediaFile> files);
}