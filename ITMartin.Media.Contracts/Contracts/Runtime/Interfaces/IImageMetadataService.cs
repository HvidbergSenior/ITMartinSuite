namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageMetadataService
{
    string GetModelFromFileName(string path);
    DateTime? GetCreationTime(string path);
    (int Width, int Height)? GetDimensions(
        string path);

    string? GetCameraModel(
        string path);

}
