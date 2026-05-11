namespace ITMartin.Media.Interfaces;

public interface IMediaDateService
{
    (DateTime? date, bool isReliable) GetBestDate(string path);
}