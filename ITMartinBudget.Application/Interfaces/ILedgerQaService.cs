namespace ITMartinBudget.Application.Interfaces;

// Backs the "❓ Spørg" box on /shop-overview - free-form questions like
// "Hvad er gennemsnitsindtægten i 2025?" answered from a compact monthly +
// category digest of the ledger, not the raw transaction list. User-triggered
// only (one click, one Haiku call), same cost profile as "Investigate".
public interface ILedgerQaService
{
    Task<string> AskAsync(string ledgerId, string question, CancellationToken cancellationToken = default);
}
