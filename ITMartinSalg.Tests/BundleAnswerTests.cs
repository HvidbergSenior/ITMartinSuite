using FluentAssertions;
using ITMartinSalg.Domain;
using ITMartinSalg.Infrastructure.Persistence;
using ITMartinVarer.Contracts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace ITMartinSalg.Tests;

// "Hvad vil du have for det hele?" (2026-11-16): mængderabat, then a random 5-15 % extra, down to the nearest 5 kr, incl. moms.
public class BundleAnswerTests
{
    [TestCase(17, 15)] [TestCase(20, 20)] [TestCase(24.9, 20)] [TestCase(3.6, 3)] [TestCase(0.4, 1)]
    public void A_price_goes_down_to_the_nearest_5(decimal kr, int told) => RabatRules.Round(kr).Should().Be(told);

    [Test]
    public void Ten_books_with_12_percent_extra()
    {
        // 10 books at 25 kr = 250, mængderabat 20 % = 200, 12 % extra = 176 -> 175.
        RabatRules.BundleKr(200, 12).Should().Be(175);
        RabatRules.ExtraFrom.Should().Be(5);
        RabatRules.ExtraTo.Should().Be(15);
    }

    [Test]
    public void An_automatic_answer_marks_the_bundle_answered()
    {
        var b = BundleOffer.Make("12345678", [1, 2], null, DateTime.UtcNow);
        b.AnswerAutomatically(175, 12);
        (b.AnswerKr, b.ExtraPercent, b.Status).Should().Be((175, 12, BundleOffer.Answered));
    }

    // An old salg.db (Bundles without the answer columns) gets them at start.
    [Test]
    public void An_old_database_gets_the_new_columns()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """CREATE TABLE "Bundles" ("Id" INTEGER PRIMARY KEY AUTOINCREMENT, "Customer" TEXT NOT NULL, "ItemIds" TEXT NOT NULL, "Kr" INTEGER NULL, "Status" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL)""";
            cmd.ExecuteNonQuery();
        }
        using var db = new TestDbFactory(conn).CreateDbContext();
        SalgDbInitializer.Upgrade(db);
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Bundles') WHERE name IN ('AnswerKr', 'ExtraPercent')";
        Convert.ToInt32(check.ExecuteScalar()).Should().Be(2);
    }
}
