using ITMartinMusicHelper.Application.Helpers;
using ITMartinMusicHelper.Application.Interfaces;
using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Services;

public class StructureService : IStructureService
{
    public List<SongStructure> GetStructures()
    {
        return new List<SongStructure>
        {
            new SongStructure
            {
                Name = "Pop Structure",
                Sections = new() { "Intro", "Verse", "Chorus", "Bridge", "Outro" },
                Suggestions = new()
                {
                    { "Intro", "Soft picking" },
                    { "Verse", "Keep it simple" },
                    { "Chorus", "Strum stronger" },
                    { "Bridge", "Add tension" },
                    { "Outro", "Finish softly" }
                },
                SectionVariations = new()
                {
                    { "Verse", chords => ProgressionVariations.DropLast(chords) },
                    { "Chorus", chords => ProgressionVariations.LiftChords(chords, 2) },
                    { "Bridge", ProgressionVariations.Invert }
                },
                SectionBass = new()
                {
                    { "Intro", "E" },
                    { "Verse", "A" },
                    { "Chorus", "E" },
                    { "Bridge", "D" },
                    { "Outro", "E" }
                }
            },

            new SongStructure
            {
                Name = "Ballad",
                Sections = new() { "Intro", "Verse", "Chorus", "Verse", "Chorus", "Bridge", "Chorus", "Outro" },
                Suggestions = new()
                {
                    { "Intro", "Soft fingerpicking, slow tempo" },
                    { "Verse", "Keep chords simple" },
                    { "Chorus", "Strum gently but stronger than verse" },
                    { "Bridge", "Add minor variation" },
                    { "Outro", "Slow fade out" }
                },
                SectionVariations = new()
                {
                    { "Verse", chords => ProgressionVariations.AddPassingChord(chords, "D") },
                    { "Chorus", chords => ProgressionVariations.LiftChords(chords, 1) },
                    { "Bridge", ProgressionVariations.Invert }
                },
                SectionBass = new()
                {
                    { "Intro", "E" },
                    { "Verse", "A" },
                    { "Chorus", "E" },
                    { "Bridge", "D" },
                    { "Outro", "E" }
                }
            }
        };
    }
}