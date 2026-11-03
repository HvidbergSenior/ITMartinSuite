using ITMartinLager.Application;
using ITMartinLager.Domain;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartinLager.Infrastructure.External;

/// <summary>Cover fingerprints with ImageSharp (2026-11-04, the free picture matching): our picture from disk, a listing's
/// picture from its site (at most 3 MB, 10 s). Any failure = no fingerprint, never an error.</summary>
public sealed class CoverPrinter(HttpClient http, ILogger<CoverPrinter> log) : ICoverPrinter
{
    private const int MaxBytes = 3 * 1024 * 1024;

    public CoverPrint? Of(byte[] jpeg)
    {
        try
        {
            using var img = Image.Load<Rgb24>(jpeg);
            using var small = img.Clone(x => x.Resize(4, 4));
            var layout = new byte[48];
            small.ProcessPixelRows(rows =>
            {
                for (var y = 0; y < 4; y++)
                {
                    var row = rows.GetRowSpan(y);
                    for (var x = 0; x < 4; x++)
                    {
                        var i = (y * 4 + x) * 3;
                        (layout[i], layout[i + 1], layout[i + 2]) = (row[x].R, row[x].G, row[x].B);
                    }
                }
            });
            img.Mutate(x => x.Resize(9, 8).Grayscale());
            var grey = new byte[9, 8];
            img.ProcessPixelRows(rows =>
            {
                for (var y = 0; y < 8; y++)
                {
                    var row = rows.GetRowSpan(y);
                    for (var x = 0; x < 9; x++) grey[x, y] = row[x].R;
                }
            });
            return new CoverPrint(CoverPrints.Shape(grey), layout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    public async Task<CoverPrint?> OfUrlAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https") return null;
        try
        {
            using var res = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode || res.Content.Headers.ContentLength > MaxBytes) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length > MaxBytes ? null : Of(bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogInformation("Cover picture not fetched: {Message}", ex.Message);
            return null;
        }
    }
}
