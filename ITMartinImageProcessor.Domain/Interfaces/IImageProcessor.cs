namespace ITMartinImageProcessor.Domain.Interfaces;

public interface IImageProcessor
{
    Task ProcessAsync(string inputPath, string outputPath);
}