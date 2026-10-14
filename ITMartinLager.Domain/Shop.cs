namespace ITMartinLager.Domain;

// The shop on bogshoppen.dk (user 2026-10-15: "people search products ... here the links and so on can be seen. So
// sales should be instant buy or bet"). Lager owns the stock, so the rules for what is shown and what can be bought
// live here; bogshoppen.dk only shows what Lager's shop API answers.
public static class Shop
{
    /// <summary>Suggested shop prices from this amount up need a person's price before Køb nu - a wrong match on an
    /// expensive item must never be sold cheaply (Lager__ShopAutoMaxKr).</summary>
    public const int DefaultAutoMaxKr = 200;

    /// <summary>Under this the AI was unsure what the thing is (Item.Confidence) - a person checks it first.</summary>
    public const double MinConfidence = 0.6;

    /// <summary>Shown in the shop: in stock or for sale, not on an auction, not sold.</summary>
    public static bool Listed(Item i) =>
        (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg) && i.Quantity > 0 && i.Title.Trim() != "";

    /// <summary>The Køb nu price: the price a person set, else the suggested shop price (10 % under the other sites)
    /// when it is under autoMaxKr and the AI was sure what the thing is. Null = no Køb nu yet.</summary>
    public static int? BuyNowKr(Item i, int autoMaxKr)
    {
        if (i.Price is > 0) return (int)Math.Round(i.Price.Value);
        if (i.PriceHint is > 0 && i.PriceHint < autoMaxKr && i.Confidence >= MinConfidence) return (int)Math.Round(i.PriceHint.Value);
        return null;
    }

    /// <summary>The proof for the price promise: the cheapest listing per site, exact matches only, cheapest first.</summary>
    public static List<PriceFind> Proof(IEnumerable<PriceFind> finds, int max = 6) =>
        finds.Where(f => !f.Similar && f.Kr > 0 && f.Url.StartsWith("http"))
            .GroupBy(f => (f.Source, f.IsNew))
            .Select(g => g.OrderBy(f => f.Kr).First())
            .OrderBy(f => f.IsNew).ThenBy(f => f.Kr)
            .Take(max).ToList();
}

public static class OrderStatuses
{
    public const string Pending = "Afventer betaling", Paid = "Betalt", Expired = "Udløbet";
}

/// <summary>A Køb nu: one copy of one item, paid through Stripe Checkout. While it is pending, the copy is reserved.</summary>
public sealed class Order
{
    /// <summary>Stripe's shortest checkout lifetime is 30 minutes.</summary>
    public static readonly TimeSpan PayWithin = TimeSpan.FromMinutes(30);

    public int Id { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");   // in the buyer's return link - ids are guessable
    public int ItemId { get; set; }
    public string Line { get; set; } = "";       // what was bought, as it was named then
    public int Kr { get; set; }
    public string Status { get; set; } = OrderStatuses.Pending;
    public string CheckoutId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int PaidKr { get; set; }              // incl. shipping
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Delivery { get; set; } = "";   // "Afhentning" / "Send med posten"
    public bool Handled { get; set; }            // sent or picked up - set by a person in Lager

    public bool Reserves(DateTime utcNow) => Status == OrderStatuses.Pending && ExpiresAt > utcNow;

    /// <summary>Paid: the buyer's details are kept, and one copy leaves the stock (the last one = Solgt).</summary>
    public void Pay(Item item, PaymentState s, DateTime utcNow)
    {
        if (Status == OrderStatuses.Paid) return;
        Status = OrderStatuses.Paid;
        PaidAt = utcNow;
        PaidKr = s.PaidKr;
        (Email, Name, Phone, Address, Delivery) = (s.Email, s.Name, s.Phone, s.Address, s.Delivery);
        item.Quantity = Math.Max(0, item.Quantity - 1);
        if (item.Quantity == 0)
        {
            item.Status = Statuses.Solgt;
            item.SoldPrice = Kr;
            item.SoldTo = Name != "" ? Name : Email;
            item.SoldAt = utcNow;
            item.Quantity = 1;   // the row keeps saying what was sold
        }
        item.Note = (item.Note + $"\nSolgt i butikken {utcNow:yyyy-MM-dd} for {Kr} kr (ordre {Id})").Trim();
        item.Touch();
    }
}

/// <summary>What Stripe says about a checkout.</summary>
public sealed record PaymentState(bool Paid, bool Expired, int PaidKr = 0, string Email = "", string Name = "", string Phone = "",
    string Address = "", string Delivery = "");
