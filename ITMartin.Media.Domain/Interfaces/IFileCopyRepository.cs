namespace ITMartin.Media.Interfaces;

public interface IFileCopyRepository
{
    void Copy(string sourcePath, string destinationPath);
    void CreateDirectory(string path);
}