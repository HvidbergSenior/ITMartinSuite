using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class OcrStateProcessor
{
    public bool IsProcessed(
        MediaFile file)
    {
        return file.OcrProcessed;
    }

    public void SetProcessed(
        MediaFile file,
        bool value)
    {
        file.OcrProcessed =
            value;
    }
}