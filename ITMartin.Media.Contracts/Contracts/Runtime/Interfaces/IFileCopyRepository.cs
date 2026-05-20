namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IFileCopyRepository
{
    void Copy(string sourcePath, string destinationPath);
    void CreateDirectory(string path);
}