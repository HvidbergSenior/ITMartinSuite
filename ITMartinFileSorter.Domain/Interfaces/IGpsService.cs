namespace ITMartinFileSorter.Domain.Interfaces;

public interface IGpsService
{
    (double lat, double lng)? GetCoordinates(string path);
}