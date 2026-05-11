using ITMartin.Media.Entities;

namespace ITMartin.Media.Interfaces;

public interface IMediaClassificationService
{
    void Classify(MediaFile file);
}