namespace ITMartinImageProcessor.Application.Interfaces;

public interface IImageProcessor
{
    Task ProcessAsync(string inputPath, string outputPath);
}