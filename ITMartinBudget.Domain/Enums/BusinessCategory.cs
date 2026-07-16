namespace ITMartinBudget.Domain.Enums;

// Separate from Category (which is tailored to the family budget's own life -
// kids' names, family transfers, etc.) - a shop's chart of accounts is a
// different shape and shouldn't be forced into that taxonomy.
public enum BusinessCategory
{
    Unknown = 0,

    // Income
    Revenue = 1,

    // Fixed costs
    Rent = 10,
    Utilities = 11,
    Insurance = 12,
    Subscriptions = 13,

    // Operating
    PaymentProcessorFees = 20,
    Supplies = 21,
    BankFees = 22,
    Salary = 23,
    Tax = 24,

    // The core reason business and private mix in one account - money the
    // owner draws out for themselves, not a business expense.
    PrivateDraw = 30,

    Other = 999
}
