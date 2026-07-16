namespace ITMartinBudget.Infrastructure.Csv;

// One row of a raw, no-header Danish bank export (columns: account,
// account2, account3, date, description, amount, balance, info1, info2,
// note) - kept separate from BankTransaction since it's a 1:1 mirror of the
// file's positional columns, not the domain shape.
public sealed class RawBankStatementRow
{
    public string Account { get; set; } = string.Empty;
    public string Account2 { get; set; } = string.Empty;
    public string Account3 { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Info1 { get; set; } = string.Empty;
    public string Info2 { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
