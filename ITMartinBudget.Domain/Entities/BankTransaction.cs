using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    // ✅ RAW (never overwritten)
    public string Description { get; set; } = string.Empty;

    // 🔥 SINGLE SOURCE OF TRUTH
    public string NormalizedDescription { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Category Category { get; set; }
    public SubCategory SubCategory { get; set; }

    public ExpenseType ExpenseType { get; set; } = ExpenseType.Optional;

    public string? MobilePayName { get; set; }
    public Guid? ContactId { get; set; }

    public TransactionType TransactionType =>
        SubCategory switch
        {
            SubCategory.OverførselFraAndre or
                SubCategory.OverførselTilAndre or
                SubCategory.Kontooverførsel or
                SubCategory.Opsparing or
                SubCategory.Børneopsparing
                => TransactionType.Overførsel,

            SubCategory.Løn or
                SubCategory.SU or
                SubCategory.Feriepenge or
                SubCategory.OverskydendeSkat or
                SubCategory.Renter or
                SubCategory.Pengegaver
                => TransactionType.Indkomst,

            _ => Amount >= 0
                ? TransactionType.Indkomst
                : TransactionType.Udgift
        };

    public bool IsMobilePay =>
        Description.Contains("mobilepay", StringComparison.OrdinalIgnoreCase);
}