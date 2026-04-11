using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Application.Helpers;

public class TripGroup
{
    public string Name { get; set; } = "";

    public List<MediaFile> Files { get; set; } = new();

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}