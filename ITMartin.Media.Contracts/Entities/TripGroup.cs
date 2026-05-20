using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Entities;

public class TripGroup
{
    public string Name { get; set; } = "";

    public List<MediaFile> Files { get; set; } = new();

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}