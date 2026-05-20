namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoMetadataService
{
    DateTime? GetCreationTime(string path);

    string GetModelFromFileName(string path);
    TimeSpan? GetDuration(
        string path);

    (int Width, int Height)? GetDimensions(
        string path);
}