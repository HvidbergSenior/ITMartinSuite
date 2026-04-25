using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Interfaces;

public interface IPickingService
{
    List<PickingPattern> GetPatterns();
}