using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Steps.NormalizationStep;

public interface IVideoBatchService
{
    Task ConvertAllVideosAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null);
}