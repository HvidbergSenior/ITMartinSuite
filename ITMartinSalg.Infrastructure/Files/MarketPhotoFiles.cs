using ITMartinSalg.Application;

namespace ITMartinSalg.Infrastructure.Files;

/// <summary>Market sale photos as files: {data}/market/{order id}.jpg (a day at the market = a handful).</summary>
public sealed class MarketPhotoFiles(string dataDir) : IMarketPhotos
{
    private string Dir => Path.Combine(dataDir, "market");
    private string File(int orderId) => Path.Combine(Dir, $"{orderId}.jpg");

    public async Task SaveAsync(int orderId, Stream photo, CancellationToken ct)
    {
        Directory.CreateDirectory(Dir);
        await using var file = System.IO.File.Create(File(orderId));
        await photo.CopyToAsync(file, ct);
    }

    public async Task<byte[]?> GetAsync(int orderId, CancellationToken ct) =>
        System.IO.File.Exists(File(orderId)) ? await System.IO.File.ReadAllBytesAsync(File(orderId), ct) : null;
}
