using ITMartin.Media.Contracts.Contracts.Runtime.Models;

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