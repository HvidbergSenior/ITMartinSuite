namespace ITMartinBudget.Infrastructure.Csv;

// One implementation per real-world export shape. Detection works off the
// file's first line only (cheap, no need to buffer/rewind a stream) - every
// shape seen so far is distinguishable that way (raw export's first line is
// already a data row starting with an account number; Totalkonto's first
// line is the literal header "Dato;Tekst;...").
public interface IBankStatementParser
{
    bool CanParse(string firstLine);

    Task<List<NormalizedImportRow>> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
