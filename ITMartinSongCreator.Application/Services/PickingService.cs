using ITMartinSongCreator.Domain.Entities;

namespace ITMartinSongCreator.Application.Services;

public class PickingService : IPickingService
{
    public List<PickingPattern> GetPatterns()
    {
        return new List<PickingPattern>
        {
            new()
            {
                Name = "Basic Fingerpicking",
                Pattern = "Bass - 3 - 2 - 1 - 2 - 3",
                Description = "Soft and flowing"
            },
            new()
            {
                Name = "Reverse Flow",
                Pattern = "Bass - 1 - 2 - 3 - 2 - 1",
                Description = "More movement"
            }
        };
    }
}