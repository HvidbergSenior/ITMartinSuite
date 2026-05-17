using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Processors;

public class ClassificationProcessor
{
    private readonly IMediaClassificationService
        _classificationService;

    public ClassificationProcessor(
        IMediaClassificationService
            classificationService)
    {
        _classificationService =
            classificationService;
    }

    public void Process(
        MediaFile file)
    {
        try
        {
            _classificationService
                .Classify(file);
        }
        catch
        {
        }
    }

    public void ProcessBatch(
        IEnumerable<MediaFile> files)
    {
        foreach (var file in files)
        {
            Process(file);
        }
    }
}