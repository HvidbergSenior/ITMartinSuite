using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Interfaces;

public interface IMelodyService
{
    List<MelodyIdea> GetIdeas(List<string> chords);
}