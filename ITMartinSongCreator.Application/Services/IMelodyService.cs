using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public interface IMelodyService
{
    List<MelodyIdea> GetIdeas(List<string> chords);
}