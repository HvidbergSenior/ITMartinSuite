namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IMediaDateService
{
    (DateTime? date, bool isReliable) GetBestDate(string path);
}