using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileHashProcessor
{
    public bool HasHash(
        MediaFile file)
    {
        return !string.IsNullOrWhiteSpace(
            file.Hash);
    }

    public string? GetHash(
        MediaFile file)
    {
        return file.Hash;
    }

    public void SetHash(
        MediaFile file,
        string hash)
    {
        file.SetHash(hash);
    }
}