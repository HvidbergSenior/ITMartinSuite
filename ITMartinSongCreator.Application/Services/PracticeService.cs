using ITMartinSongCreator.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace ITMartinSongCreator.Application.Services;

public class PracticeService : IPracticeService
{
    public List<PracticeStep> GeneratePracticeSteps(
        ChordProgression progression,          // <- use this
        List<string> chords,
        PickingPattern pattern,
        List<MelodyIdea> melodyIdeas,
        SongStructure structure)
    {
        var steps = new List<PracticeStep>();

        foreach (var section in structure.Sections)
        {
            var sectionChords = structure.SectionVariations != null &&
                                structure.SectionVariations.TryGetValue(section, out var variationFunc)
                ? variationFunc(chords)
                : chords;

            // Get bass per chord
            var bassStrings = sectionChords.Select(c =>
                progression.BassMapping != null && progression.BassMapping.TryGetValue(c, out var b)
                    ? b
                    : "E"
            ).ToList();

            var step = new PracticeStep
            {
                Section = section,
                Chords = sectionChords,
                BassStrings = bassStrings,
                PickingPattern = pattern.Pattern,
                MelodyTips = melodyIdeas.Select(i => i.Title).ToList(),
                Note = structure.Suggestions.TryGetValue(section, out var note) ? note : null
            };

            steps.Add(step);
        }

        return steps;
    }
}