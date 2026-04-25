using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public interface IPickingService
{
    List<PickingPattern> GetPatterns();
}