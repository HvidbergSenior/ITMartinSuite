using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Interfaces;

public interface IStructureService
{
    List<SongStructure> GetStructures();
}