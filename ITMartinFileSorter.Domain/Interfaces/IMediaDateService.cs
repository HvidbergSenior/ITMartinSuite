namespace ITMartinFileSorter.Domain.Interfaces;

public interface IMediaDateService
{
    DateTime? GetBestDate(string path);
}