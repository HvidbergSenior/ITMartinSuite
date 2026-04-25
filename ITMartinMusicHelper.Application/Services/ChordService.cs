using ITMartinMusicHelper.Application.Interfaces;
using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Services;

public class ChordService : IChordService
{
    public List<ChordProgression> GetProgressions()
    {
        return new List<ChordProgression>
        {
            new()
            {
                Name = "Em - C - G - D",
                Mood = "Emotional",
                Chords = new() { "Em", "C", "G", "D" },
                BassMapping = new Dictionary<string, string>
                {
                    { "Em", "E" },
                    { "C", "A" },
                    { "G", "E" },
                    { "D", "G" }
                }
            },
            new()
            {
                Name = "G - D - Em - C",
                Mood = "Happy",
                Chords = new() { "G", "D", "Em", "C" },
                BassMapping = new Dictionary<string, string>
                {
                    { "G", "E" },
                    { "D", "G" },
                    { "Em", "E" },
                    { "C", "A" },
                }
                
            },
            new()
            {
                Name = "Am - F - C - G",
                Mood = "Sad",
                Chords = new() { "Am", "F", "C", "G" },
                BassMapping = new Dictionary<string, string>
                {
                    { "Am", "A" },
                    { "F", "E" },
                    { "C", "A" },
                    { "G", "E" }
                }
            }
        };
    }
}