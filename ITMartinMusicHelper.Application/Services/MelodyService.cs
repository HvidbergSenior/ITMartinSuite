using ITMartinMusicHelper.Application.Interfaces;
using ITMartinMusicHelper.Domain.Entities;

namespace ITMartinMusicHelper.Application.Services;

public class MelodyService : IMelodyService
{
    public List<MelodyIdea> GetIdeas(List<string> chords)
    {
        var ideas = new List<MelodyIdea>();

        int index = 0;

        foreach (var chord in chords)
        {
            ideas.Add(new MelodyIdea
            {
                Id = $"lift-{index}",
                Title = $"Lift finger in {chord}",
                Description = $"Try lifting a finger slightly in {chord}",
                TargetChord = chord
            });

            ideas.Add(new MelodyIdea
            {
                Id = $"hammer-{index}",
                Title = $"Hammer-on in {chord}",
                Description = $"Add a hammer-on while playing {chord}",
                TargetChord = chord
            });

            index++;
        }
        
        // General ideas
        ideas.Add(new MelodyIdea
        {
            Id = "high-string",
            Title = "High string fill",
            Description = "Play the high E string between picking notes"
        });

        ideas.Add(new MelodyIdea
        {
            Id = "pause-groove",
            Title = "Pause for groove",
            Description = "Skip a note occasionally to create rhythm variation"
        });

        return ideas;
    }
}