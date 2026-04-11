namespace ITMartinFileSorter.Domain.Interfaces;

public interface IFileCopyRepository
{
    void Copy(string sourcePath, string destinationPath);
    void CreateDirectory(string path);
}