using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.Helpers;

namespace ITMartinFileSorter.Infrastructure.Services;

public class GpsService : IGpsService
{
    public (double lat, double lng)? GetCoordinates(string path)
    {
        return GpsHelper.GetCoordinates(path);
    }
}