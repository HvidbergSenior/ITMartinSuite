using System.Text.Json;
using FluentAssertions;
using ITMartinTilbud.Domain;
using ITMartinTilbud.Infrastructure;

namespace ITMartinTilbud.Tests;

// Salling Anti Food Waste answer in the published shape (no key yet, so not a captured live answer):
// biggest discount first, sold-out and expired markdowns left out, brand and stock unit in Danish.
public class FoodWasteTests
{
    private const string Json = """
    [ { "store": { "name": "Netto Aarhus C", "brand": "netto", "address": { "street": "Vestergade 1", "zip": "8000", "city": "Aarhus C" } },
        "clearances": [
          { "offer": { "newPrice": 10, "originalPrice": 20, "percentDiscount": 50, "stock": 3, "stockUnit": "each", "endTime": "2026-10-06T21:00:00Z" },
            "product": { "description": "Hakket oksekød 500 g", "image": "https://x/1.jpg" } },
          { "offer": { "newPrice": 15, "originalPrice": 20, "percentDiscount": 25, "stock": 2, "stockUnit": "each", "endTime": "2026-10-06T21:00:00Z" },
            "product": { "description": "Rugbrød" } },
          { "offer": { "newPrice": 5, "originalPrice": 10, "percentDiscount": 50, "stock": 0, "stockUnit": "each", "endTime": "2026-10-06T21:00:00Z" },
            "product": { "description": "Udsolgt yoghurt" } },
          { "offer": { "newPrice": 5, "originalPrice": 10, "percentDiscount": 50, "stock": 1, "stockUnit": "kg", "endTime": "2026-10-05T21:00:00Z" },
            "product": { "description": "I går" } } ] } ]
    """;

    [Test]
    public void Biggest_discount_first_without_sold_out_or_expired()
    {
        var list = SallingFoodWaste.Parse(JsonDocument.Parse(Json).RootElement, new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        list.Select(c => c.Title).Should().Equal("Hakket oksekød 500 g", "Rugbrød");
        list[0].Brand.Should().Be("Netto");
        Clearances.Readable("40+ ØKO.RYGEOST LØGISMOSE").Should().Be("40+ øko.rygeost løgismose");
        list[0].StockUnit.Should().Be("stk");
        list[0].Address.Should().Be("Vestergade 1, 8000 Aarhus C");
    }
}
