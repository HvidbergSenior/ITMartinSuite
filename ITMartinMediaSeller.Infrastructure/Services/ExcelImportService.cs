using ClosedXML.Excel;
using ITMartinMediaSeller.Domain.Entities;

namespace ITMartinMediaSeller.Infrastructure.Services;

public class ExcelImportService
{
    public List<MediaItem> Import(string path, string format)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();

        var items = new List<MediaItem>();

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var title = row.Cell(1).GetString();

            if (string.IsNullOrWhiteSpace(title))
                continue;

            items.Add(new MediaItem
            {
                Title = title.Trim(),
                Format = format,
                Quantity = 1
            });
        }

        return items;
    }
}