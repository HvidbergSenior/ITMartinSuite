namespace ITMartinBudget.Infrastructure.Csv;

// One row of the "Totalkonto" bank export shape (Hvidberg's and ITMartin's
// bank) - headered, semicolon-delimited, UTF-8 with BOM, and unlike
// Bogshoppen's raw export the bank itself already assigns a category
// (Hovedkategori/Kategori) to every row - a real head start on categorizing
// that Bogshoppen's file never had.
public sealed class TotalkontoRow
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string MainCategory { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
