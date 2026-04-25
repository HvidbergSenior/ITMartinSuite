using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public class MelodyService : IMelodyService
{
    public List<MelodyIdea> GetIdeas(List<string> chords)
    {
        var ideas = new List<MelodyIdea>();

        foreach (var chord in chords)
        {
            ideas.Add(new MelodyIdea
            {
                Title = $"Lift finger in {chord}",
                Description = $"Try lifting a finger slightly in {chord} to create variation",
                TargetChord = chord
            });

            ideas.Add(new MelodyIdea
            {
                Title = $"Hammer-on in {chord}",
                Description = $"Add a hammer-on while playing {chord}",
                TargetChord = chord
            });
        }

        // General ideas
        ideas.Add(new MelodyIdea
        {
            Title = "High string fill",
            Description = "Play the high E string between picking notes"
        });

        ideas.Add(new MelodyIdea
        {
            Title = "Pause for groove",
            Description = "Skip a note occasionally to create rhythm variation"
        });

        return ideas;
    }
}