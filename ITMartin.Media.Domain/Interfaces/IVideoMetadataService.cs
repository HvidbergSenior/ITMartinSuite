namespace ITMartin.Media.Interfaces;

public interface IVideoMetadataService
{
    DateTime? GetCreationTime(string path);

    string GetModelFromFileName(string path);
}