using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class OcrProcessor
{
    public void Process(
        MediaFile file,
        string? text)
    {
        file.OcrText =
            text;

        file.OcrProcessed =
            true;
    }
}