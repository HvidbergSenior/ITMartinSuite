using System.IO.Compression;
using System.Text;

namespace ITMartinLager.Application;

/// <summary>Everything as CSV (Excel-friendly: semicolons + BOM) - the way out for accountants, DBA lists and backups.
/// And the item photos as a zip.</summary>
public sealed class ExportUseCases(IItemRepository items, IBookCheckRepository checks, IPhotoStore photos)
{
    /// <summary>The item photos as one zip, a folder per box (user 2026-10-19: "download item photos"). Null box = all.
    /// Written straight into the stream; returns how many photos went in.</summary>
    public async Task<int> PhotosZipAsync(Stream output, string? boxCode, CancellationToken ct = default)
    {
        var withPhoto = (await items.AllAsync(ct))
            .Where(i => i.ItemPhoto != "" && (boxCode is null || i.Box?.Code == boxCode))
            .OrderBy(i => i.Box?.Code).ThenBy(i => i.Id).ToList();
        var count = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var i in withPhoto)
        {
            if (await photos.ReadAsync(i.ItemPhoto, ct) is not { Length: > 0 } jpeg) continue;   // file gone or empty: skip, not fail
            // A JPEG is already packed - compressing it again only costs time.
            var entry = zip.CreateEntry($"{i.Box?.Code ?? "Uden kasse"}/{i.PhotoFileName()}", CompressionLevel.NoCompression);
            await using var s = entry.Open();
            await s.WriteAsync(jpeg, ct);
            count++;
        }
        return count;
    }

    private static string C(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";

    public async Task<byte[]> ItemsCsvAsync(CancellationToken ct = default)
    {
        var rows = (await items.AllAsync(ct)).OrderBy(i => i.Kind).ThenBy(i => i.Series).ThenBy(i => i.Title);
        var sb = new StringBuilder("﻿Id;Type;Titel;Kunstner;Serie;Nummer;År;Platform;Stand;Antal;Kasse;Pris;Status;Note;Det interessante;Udgave;Forlag;ISBN\n");
        foreach (var i in rows)
            sb.AppendLine(string.Join(';', i.Id, C(i.Kind), C(i.Title), C(i.Artist), C(i.Series), C(i.Number), i.Year, C(i.Platform),
                C(i.Condition), i.Quantity, C(i.Box?.Code), i.Price, C(i.Status), C(i.Note), C(i.Interest), C(i.Edition), C(i.Publisher), C(i.Barcode)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Every checked book - the list to work through when putting books up on DBA, AbeBooks, Bokbörsen.</summary>
    public async Task<byte[]> BooksCsvAsync(CancellationToken ct = default)
    {
        var rows = (await checks.AllAsync(ct)).OrderByDescending(b => b.CheckedAt);
        var sb = new StringBuilder("﻿Tjekket;Titel;Forfatter;År;ISBN;Stand;Antal;Dom;Fra kr;Til kr;Efterspørgsel;Sælg på;Grund;DBA;AbeBooks;Bokbörsen;Nemos;Faraos;Bevis;Kasse\n");
        foreach (var b in rows)
            sb.AppendLine(string.Join(';', b.CheckedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), C(b.Title), C(b.Author), b.Year, C(b.Isbn), C(b.Condition),
                b.Quantity, C(b.Verdict), b.Low, b.High, C(b.Demand), C(b.SellAt), C(b.Reason), C(b.Dba), C(b.AbeBooks), C(b.Bokborsen != "" ? b.Bokborsen : b.AmazonDe), C(b.Nemos), C(b.Faraos), C(b.Proof), C(b.BoxCode)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
