using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Infrastructure;

// Seeds a realistic 3-month "family" ledger for the demo tier — only runs
// when Budget:SeedDemoData=true (set on the demo compose service, never on
// the real budget-web pointed at the production data volume). Idempotent:
// skips entirely if the family ledger already has data, so a container
// restart never duplicates rows.
public static class DemoSeeder
{
    public static async Task SeedAsync(BudgetDbContext db)
    {
        if (await db.Transactions.AnyAsync(x => x.LedgerId == "family"))
            return;

        var today = DateTime.UtcNow.Date;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-2);

        var transactions = new List<BankTransaction>();

        for (var monthStart = start; monthStart <= today; monthStart = monthStart.AddMonths(1))
        {
            transactions.Add(Tx(monthStart.AddDays(0), "Løn A/S Demofirma", 32450m, TransactionType.Indkomst, Category.Løn, BudgetGroup.FixedIncome));

            transactions.Add(Tx(monthStart.AddDays(1), "Husleje Boligforening", -8200m, TransactionType.Udgift, Category.Husleje, BudgetGroup.RealkreditBolig));
            transactions.Add(Tx(monthStart.AddDays(2), "Forsikring Topdanmark", -645m, TransactionType.Udgift, Category.Forsikring, BudgetGroup.Forsikring));
            transactions.Add(Tx(monthStart.AddDays(3), "TDC Telefon/TV/Internet", -499m, TransactionType.Udgift, Category.TelefonTvInternet, BudgetGroup.Subscriptions));

            transactions.Add(Tx(monthStart.AddDays(4), "Netto", -412.50m, TransactionType.Udgift, Category.Dagligvarer, BudgetGroup.EverydayGrocery));
            transactions.Add(Tx(monthStart.AddDays(9), "Bilka", -687.30m, TransactionType.Udgift, Category.Dagligvarer, BudgetGroup.EverydayGrocery));
            transactions.Add(Tx(monthStart.AddDays(14), "Føtex", -298.90m, TransactionType.Udgift, Category.Dagligvarer, BudgetGroup.EverydayGrocery));
            transactions.Add(Tx(monthStart.AddDays(19), "Netto", -355.15m, TransactionType.Udgift, Category.Dagligvarer, BudgetGroup.EverydayGrocery));
            transactions.Add(Tx(monthStart.AddDays(24), "Rema 1000", -276.40m, TransactionType.Udgift, Category.Dagligvarer, BudgetGroup.EverydayGrocery));

            transactions.Add(Tx(monthStart.AddDays(6), "Shell", -450.00m, TransactionType.Udgift, Category.Braendstof, BudgetGroup.Fuel));
            transactions.Add(Tx(monthStart.AddDays(21), "Q8", -410.00m, TransactionType.Udgift, Category.Braendstof, BudgetGroup.Fuel));

            transactions.Add(Tx(monthStart.AddDays(5), "Netflix", -119.00m, TransactionType.Udgift, Category.Streaming, BudgetGroup.Subscriptions, recurringMonths: 1));
            transactions.Add(Tx(monthStart.AddDays(5), "Spotify", -99.00m, TransactionType.Udgift, Category.Streaming, BudgetGroup.Subscriptions, recurringMonths: 1));

            transactions.Add(Tx(monthStart.AddDays(11), "Café Norden", -156.00m, TransactionType.Udgift, Category.Cafe, BudgetGroup.RestaurantCafe));
            transactions.Add(Tx(monthStart.AddDays(17), "Sushi Take Away", -298.00m, TransactionType.Udgift, Category.Takeaway, BudgetGroup.RestaurantCafe));

            transactions.Add(Tx(monthStart.AddDays(13), "Matas", -189.50m, TransactionType.Udgift, Category.Sundhed, BudgetGroup.PersonalCare));
            transactions.Add(Tx(monthStart.AddDays(22), "H&M", -449.00m, TransactionType.Udgift, Category.Toej, BudgetGroup.GeneralShopping));

            transactions.Add(Tx(monthStart.AddDays(15), "Opsparing", -2000m, TransactionType.Udgift, Category.Opsparing, BudgetGroup.OverførslerTilFraOpsparingsKonto));
        }

        // One-off, non-recurring items scattered across the range — gives the
        // demo dashboard some visible month-to-month variation instead of a
        // perfectly identical pattern every month.
        transactions.Add(Tx(start.AddDays(27), "Elgiganten", -3299m, TransactionType.Udgift, Category.Elektronik, BudgetGroup.GeneralShopping));
        transactions.Add(Tx(start.AddMonths(1).AddDays(8), "Fødselsdagsgave", -350m, TransactionType.Udgift, Category.Gaver, BudgetGroup.GiftExpense));
        transactions.Add(Tx(start.AddMonths(1).AddDays(18), "Skat Overskydende Skat", 1840m, TransactionType.Indkomst, Category.Skat, BudgetGroup.Refund));
        transactions.Add(Tx(today.AddDays(-3), "Fri Biograf", -178m, TransactionType.Udgift, Category.KoncertBio, BudgetGroup.Entertainment));

        db.Transactions.AddRange(transactions);

        db.PlannedTransactions.AddRange(
            new PlannedTransaction
            {
                ExpectedDate = today.AddMonths(1),
                Description = "Bilservice — årligt eftersyn",
                Amount = -2400m,
                TransactionType = TransactionType.Udgift,
                Category = Category.BilVedligehold,
                BudgetGroup = BudgetGroup.CarMaintenance,
                Title = "Bilservice",
            },
            new PlannedTransaction
            {
                ExpectedDate = today.AddMonths(2),
                Description = "Sommerferie — depositum",
                Amount = -1500m,
                TransactionType = TransactionType.Udgift,
                Category = Category.RejserUdflugter,
                BudgetGroup = BudgetGroup.Traveling,
                Title = "Sommerferie",
            });

        db.CategoryRules.AddRange(
            new CategoryRule { LedgerId = "family", Pattern = "Netto", CategoryName = "Dagligvarer", Scope = TransactionScope.Unknown },
            new CategoryRule { LedgerId = "family", Pattern = "Netflix", CategoryName = "Streaming", Scope = TransactionScope.Unknown });

        await db.SaveChangesAsync();
    }

    private static BankTransaction Tx(
        DateTime date, string description, decimal amount, TransactionType type,
        Category category, BudgetGroup group, decimal recurringMonths = 0) => new()
    {
        Date = date,
        Description = description,
        NormalizedDescription = description,
        Amount = amount,
        TransactionType = type,
        Category = category,
        BudgetGroup = group,
        Title = description,
        LedgerId = "family",
        RecurringIntervalMonths = recurringMonths,
    };
}
