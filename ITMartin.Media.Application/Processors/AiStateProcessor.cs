using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class AiStateProcessor
{
    public bool IsProcessed(
        MediaFile file)
    {
        return file.AiProcessed;
    }

    public void SetProcessed(
        MediaFile file,
        bool value)
    {
        file.AiProcessed =
            value;
    }
}