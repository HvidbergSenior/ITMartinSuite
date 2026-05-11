using ITMartin.Media.Entities;

namespace ITMartinFileSorter.Application.Interfaces;

public interface IHomeLocationService
{
    (double lat, double lng)? DetectHome(IEnumerable<MediaFile> files);

    bool IsNearHome(
        (double lat, double lng) home,
        (double lat, double lng) point);
}