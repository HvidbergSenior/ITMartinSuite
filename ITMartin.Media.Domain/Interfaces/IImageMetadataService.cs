namespace ITMartin.Media.Interfaces;

public interface IImageMetadataService
{
    string GetModelFromFileName(string path);
    DateTime? GetCreationTime(string path);
}
