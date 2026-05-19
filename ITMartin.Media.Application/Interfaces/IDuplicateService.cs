using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Interfaces;

public interface IDuplicateService
{
        List<DuplicateGroup> BuildDuplicateGroups(
            IReadOnlyCollection<MediaFile> files);
}