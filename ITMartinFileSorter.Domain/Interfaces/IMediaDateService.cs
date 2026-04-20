namespace ITMartinFileSorter.Domain.Interfaces;

public interface IMediaDateService
{
    (DateTime? date, bool isReliable) GetBestDate(string path);
}