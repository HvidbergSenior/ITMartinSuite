using ITMartinSongCreator.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace ITMartinSongCreator.Application.Services;

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
