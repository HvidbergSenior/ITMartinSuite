namespace ITMartinFileSorter.Application.Helpers;


public enum GroupingStrategy
{
    None,
    ByDate,
    ByLocation,
    ByDevice,
    ByMediaType,
    CustomPrefix,
    ByTrip   // ⭐ NEW
}