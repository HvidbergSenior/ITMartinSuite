using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class AiDescriptionProcessor
{
    public void Process(
        MediaFile file,
        string? description,
        float? confidence = null)
    {
        file.AiDescription =
            description;

        file.AiConfidence =
            confidence;

        file.AiProcessed =
            true;
    }
}