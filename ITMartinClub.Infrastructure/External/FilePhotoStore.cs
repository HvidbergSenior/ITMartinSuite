using ITMartinClub.Application;

namespace ITMartinClub.Infrastructure.External;

// Photos on the board, Find det and Påmindelser: data/photos/{guid}.jpg, served by /photo/{name}.
internal sealed class FilePhotoStore(string root) : IPhotoStore
{
    public async Task<string> SaveAsync(byte[] jpeg, CancellationToken ct = default)
    {
        var name = $"{Guid.NewGuid():N}.jpg";
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, name), jpeg, ct);
        return name;
    }
}
