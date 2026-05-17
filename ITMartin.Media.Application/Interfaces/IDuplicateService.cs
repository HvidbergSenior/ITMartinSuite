using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Interfaces;

public interface IDuplicateService
{
    string FolderPath { get; set; }

    List<MediaFile> AllFiles { get; set; }

    List<AiCollection> AiCollections { get; set; }

    List<DuplicateGroup> DuplicateGroups { get; set; }

    void Reset();

    void BuildDuplicateGroups();
}