namespace ITMartinFileSorter.Application.Services;

public class OrganizerOptions
{
    public bool DetectTrips { get; set; } = true;
    public bool DetectEvents { get; set; } = true;
    public bool DetectHome { get; set; } = true;

    public bool RemoveScreenshots { get; set; } = true;
    public bool RemoveBlurry { get; set; } = false;

    public bool RenameFiles { get; set; } = true;
    public bool CopyOriginals { get; set; } = true;
}