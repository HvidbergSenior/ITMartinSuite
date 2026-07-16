namespace ITMartinBudget.Infrastructure.Csv;

// The one shape every bank-export parser converts into, regardless of the
// source file's own layout (raw no-header CSV, headered "Totalkonto" CSV,
// and eventually other shapes/formats) - so there's exactly one place
// (LedgerImportService) that does scope classification, category-rule
// application, dedup and saving, instead of duplicating that per format.
public sealed record NormalizedImportRow(
    DateTime Date,
    string Description,
    decimal Amount,
    decimal? Balance,
    string RawDetails,
    // Only set when the source file already carries the bank's own
    // categorization (e.g. Totalkonto's Kategori column) - used to seed a
    // CategoryRule automatically on first import, so a well-categorized
    // export needs little to no manual work on /shop-categorize. Null for
    // formats that don't provide this (e.g. Bogshoppen's raw export).
    string? SuggestedCategoryName);
