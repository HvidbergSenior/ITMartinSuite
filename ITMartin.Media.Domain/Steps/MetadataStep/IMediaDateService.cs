namespace ITMartin.Media.Domain.Steps.MetadataStep;

public interface IMediaDateService
{
    (DateTime? date, bool isReliable) GetBestDate(string path);
}