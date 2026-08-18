using ITMartin.Receipt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Receipt.Infrastructure;

// Minimal demo-tier seed - a handful of realistic receipts so a visitor sees
// a populated app immediately. Only runs when Receipt:SeedDemoData=true (the
// demo compose service). Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(ReceiptDbContext db)
    {
        if (await db.Transactions.AnyAsync())
            return;

        db.Transactions.AddRange(
            new ReceiptTransaction
            {
                Id = Guid.NewGuid(),
                MerchantName = "Netto",
                PurchaseDate = DateTime.UtcNow.AddDays(-2),
                TotalAmount = 412.50m,
                VatAmount = 82.50m,
                Items =
                [
                    new ReceiptTransactionItem { Description = "Øko mælk 1L", OriginalPrice = 12.95m },
                    new ReceiptTransactionItem { Description = "Rugbrød", OriginalPrice = 24.95m },
                    new ReceiptTransactionItem { Description = "Kylling filet 500g", OriginalPrice = 45.00m, DiscountAmount = 10.00m, DiscountType = "Tilbud" },
                    new ReceiptTransactionItem { Description = "Æbler 1kg", OriginalPrice = 18.95m },
                ],
            },
            new ReceiptTransaction
            {
                Id = Guid.NewGuid(),
                MerchantName = "Bilka",
                PurchaseDate = DateTime.UtcNow.AddDays(-8),
                TotalAmount = 687.30m,
                VatAmount = 137.46m,
                Items =
                [
                    new ReceiptTransactionItem { Description = "Vaskepulver", OriginalPrice = 89.00m },
                    new ReceiptTransactionItem { Description = "Toiletpapir 24-pak", OriginalPrice = 129.00m },
                    new ReceiptTransactionItem { Description = "Kaffe 500g", OriginalPrice = 49.95m, IsSuspicious = true, RawText = "KAFFE 500G ?49,95" },
                ],
            },
            new ReceiptTransaction
            {
                Id = Guid.NewGuid(),
                MerchantName = "Matas",
                PurchaseDate = DateTime.UtcNow.AddDays(-15),
                TotalAmount = 189.50m,
                VatAmount = 37.90m,
                Items =
                [
                    new ReceiptTransactionItem { Description = "Shampoo", OriginalPrice = 79.95m },
                    new ReceiptTransactionItem { Description = "Tandpasta", OriginalPrice = 34.95m },
                ],
            });

        await db.SaveChangesAsync();
    }
}
