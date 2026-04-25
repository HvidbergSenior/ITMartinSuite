using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public interface IStructureService
{
    List<SongStructure> GetStructures();
}