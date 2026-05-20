namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IGpsService
{
    (double lat, double lng)? GetCoordinates(string path);
}