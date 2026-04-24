namespace ITMartinImageProcessor.Domain.Interfaces;

public interface IFileService
{
    IEnumerable<string> GetImages(string folder);
    DateTime GetCreated(string path);
    void Move(string source, string destination);
}