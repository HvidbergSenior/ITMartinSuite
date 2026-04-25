using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Interfaces;

public interface IChordService
{
    List<ChordProgression> GetProgressions();
}