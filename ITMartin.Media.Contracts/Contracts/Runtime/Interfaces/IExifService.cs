namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IExifService
{
    (int? Width, int? Height) GetDimensions(string path);

    (string? Make, string? Model, string? Software)? ReadMetadata(string path);

    bool IsIphone(string path);

    bool IsAndroid(string path);
}