using System.Globalization;
using System.Text;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.LibraryFinishing;

// Pure formatting, no I/O - kept separate from LibraryFinishingService so the
// Markdown content can be tested directly against a hand-built report
// without running the real (slow, filesystem-heavy) service. "make a
// description that I can read after run" (2026-09-07) - this is that
// description; LibraryFinishingService writes its output to a file in the
// library, e.g. as Kørselsrapport.md.
public static class LibraryFinishingReportFormatter
{
    public static string ToMarkdown(LibraryFinishingReport report)
    {
        var sb = new StringBuilder();
        var duration = report.FinishedAtUtc is { } finished
            ? finished - report.StartedAtUtc
            : (TimeSpan?)null;

        sb.AppendLine("# Kørselsrapport");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Bibliotek: `{report.LibraryPath}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Startet: {report.StartedAtUtc:yyyy-MM-dd HH:mm} UTC");
        if (duration is { } d)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Varighed: {FormatDuration(d)}");
        sb.AppendLine();

        if (report.PhaseErrors.Count > 0)
        {
            sb.AppendLine("## ⚠ Fejl undervejs");
            sb.AppendLine();
            sb.AppendLine("Kørslen fortsatte, men disse trin fejlede og bør tjekkes manuelt:");
            sb.AppendLine();
            foreach (var error in report.PhaseErrors)
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {error}");
            sb.AppendLine();
        }

        sb.AppendLine("## Oprydning");
        sb.AppendLine();
        if (report.OrientationFix is { } orientation)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Rotation: {orientation.PhotosChecked} tjekket, {orientation.PhotosRotated} rettet, {orientation.NeedsManualReview.Count} kræver manuelt tjek");
        if (report.BurstsFlattened is { } bursts)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Burst-mapper udpakket: {bursts.FoldersFlattened} mapper, {bursts.FilesMoved} filer");
        if (report.AlbumArtReclassified is { } albumArt)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Albumcovers omklassificeret: {albumArt.MovedHighConfidence} flyttet, {albumArt.ReviewCandidates.Count} til manuel gennemgang");
        if (report.WebWatermarksReclassified is { } watermarks)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Web-vandmærker fjernet: {watermarks.Moved} af {watermarks.Checked} tjekket");
        if (report.SmallAlbumsPruned is { } albums)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Små album flyttet til SmåAlbummer/: {albums.AlbumsRemoved} album ({albums.FilesRemoved} filer)");
        sb.AppendLine();

        sb.AppendLine("## Indeks");
        sb.AppendLine();
        if (report.IndexReport is { } index)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {index.DoneFiles} / {index.TotalFiles} filer klar, over {report.IndexConvergeIterations} omgang(e)");
        sb.AppendLine();

        sb.AppendLine("## Dubletter");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Kategorier tjekket: {string.Join(", ", report.CategoryFoldersDeduped)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- {report.DuplicatesMoved} dubletter flyttet til Dubletter/ (ud af {report.FilesCheckedForDuplicates} tjekket) - originalerne er der stadig, intet er slettet");
        sb.AppendLine();

        sb.AppendLine("## Tilføjelser (SmartFolders)");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Ture fundet: {report.Trips.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Traditioner fundet: {report.Traditions.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Årbøger genereret: {report.Yearbooks.Count} ({string.Join(", ", report.Yearbooks.Select(y => y.Year))})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Ukendte personer grupperet: {report.UnknownPersonFolders.Count} (kan navngives manuelt bagefter)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Lignende scener grupperet: {report.SimilarSceneFolders.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Galleri-samlinger synkroniseret: {(report.GalleryCollectionsSynced ? "ja" : "nej")}");
        sb.AppendLine();

        sb.AppendLine("## Galleri-eksport");
        sb.AppendLine();
        if (report.GalleryExport is { } gallery)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {gallery.TotalFiles} filer, {gallery.ThumbnailsGenerated} miniaturer, {gallery.YearsGenerated} år → `{gallery.IndexPath}`");
        sb.AppendLine();

        sb.AppendLine("## Leveringskontrol");
        sb.AppendLine();
        if (report.IntegrityReport is { } integrity)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Integritet: {integrity.TotalFilesChecked} filer tjekket, {integrity.FailureCount} fejl");
        if (report.StructureReport is { } structure)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Struktur: {structure.ExpectedFoldersFound.Count} mapper fundet, {structure.ExpectedFoldersMissing.Count} mangler, {structure.Issues.Count} problem(er)");
        if (report.CollectionsRepair is { } repair)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- collections.json: {repair.NormalizedPaths} normaliseret, {repair.RemovedMissingPaths} fjernet (pegede på filer der ikke findes)");
        if (report.DeliveryStructureReport is { } delivery)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Leveringsstruktur: {delivery.YearFoldersChecked} år-mapper tjekket, {delivery.Issues.Count} problem(er)");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string FormatDuration(TimeSpan d) =>
        d.TotalHours >= 1
            ? $"{(int)d.TotalHours}t {d.Minutes}m"
            : $"{d.Minutes}m {d.Seconds}s";
}
