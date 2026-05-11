namespace ITMartin.Media.Interfaces;

public interface IGpsService
{
    (double lat, double lng)? GetCoordinates(string path);
}