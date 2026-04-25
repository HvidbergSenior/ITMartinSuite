using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public interface IChordService
{
    List<ChordProgression> GetProgressions();
}