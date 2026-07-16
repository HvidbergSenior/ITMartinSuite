namespace ITMartinBudget.Domain.Entities;

// A user's own answer to "what is this recurring thing, really" - e.g.
// mapping the pattern "faetex" (normalized "Føtex") to category name
// "Dagligvarer" and Scope.Private. Saved once, applied automatically to every
// matching transaction (past and future) in that ledger, so a customer never
// has to re-categorize the same recurring bill twice.
public class CategoryRule
{
    public int Id { get; set; }

    public string LedgerId { get; set; } = string.Empty;

    // Matches BankTransaction.NormalizedDescription - the smallest natural
    // clustering unit ("Føtex", "Husleje"), not a broad pre-set category, so
    // the user decides how fine or coarse to combine things.
    public string Pattern { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public Enums.TransactionScope Scope { get; set; } = Enums.TransactionScope.Unknown;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
