using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Interfaces;

public interface IPracticeService
{
    List<PracticeStep> GeneratePracticeSteps(
        ChordProgression progression,          // <- pass the progression
        List<string> chords,
        PickingPattern pattern,
        List<MelodyIdea> melodyIdeas,
        SongStructure structure
    );
}
