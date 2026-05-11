using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Interfaces;

public interface IMediaClassificationService
{
    void Classify(MediaFile file);
}