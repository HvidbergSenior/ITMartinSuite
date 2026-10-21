namespace ITMartinSalg.Domain;

// The shop on bogshoppen.dk (user 2026-10-15: "people search products ... So sales should be instant buy or bet").
// Split out of Lager 2026-10-21 ("one app one responsible"): Lager owns the things and whether they are for sale; Salg
// owns what they cost today, the orders and the payment. bogshoppen.dk only shows what Salg's shop API answers.
public static class Shop
{
    /// <summary>Suggested shop prices from this amount up need a person's price before Køb nu - a wrong match on an
    /// expensive item must never be sold cheaply (Salg__ShopAutoMaxKr).</summary>
    public const int DefaultAutoMaxKr = 200;

    /// <summary>Under this the AI was unsure what the thing is (StockItem.Confidence) - a person checks it first.</summary>
    public const double MinConfidence = 0.6;

    /// <summary>The Køb nu price: the price a person set, else the suggested shop price (10 % under the other sites)
    /// when it is under autoMaxKr and the AI was sure what the thing is. Null = no Køb nu yet.</summary>
    public static int? BuyNowKr(StockItem i, int autoMaxKr)
    {
        if (i.PriceKr is > 0) return i.PriceKr;
        if (i.HintKr is > 0 && i.HintKr < autoMaxKr && i.Confidence >= MinConfidence) return i.HintKr;
        return null;
    }
}

public static class SaleChannels
{
    public const string BuyNow = "Køb nu", Auction = "Auktion", Market = "Loppemarked";
}

/// <summary>A sale: one or more copies, paid through Stripe Checkout (Køb nu). While it is pending, the copies are
/// reserved. After payment it goes Betalt -> Pakket -> Sendt (or Afhentet) - only forwards, one step at a time
/// (user 2026-10-20: "sold, packed, in storage or something"). Once paid, Lager is told the copies are sold
/// (StockBooked) - until Lager has said OK, it is tried again.</summary>
public sealed class Order
{
    /// <summary>Stripe's shortest checkout lifetime is 30 minutes.</summary>
    public static readonly TimeSpan PayWithin = TimeSpan.FromMinutes(30);

    public int Id { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");   // in the buyer's return link - ids are guessable
    public int ItemId { get; set; }              // the first line's item (orders before baskets had only this)
    public string Line { get; set; } = "";       // what was bought, as it was named then ("Mort + 2 andre")
    public int Kr { get; set; }                  // the total of the lines, offers included
    public string Status { get; set; } = OrderStatuses.Pending;
    public string Channel { get; set; } = SaleChannels.BuyNow;
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
    public DateTime? PackedAt { get; set; }
    public DateTime? DoneAt { get; set; }        // sent or picked up
    public string Tracking { get; set; } = "";   // the parcel's track-and-trace number
    /// <summary>Lager has taken the paid copies out of the stock.</summary>
    public bool StockBooked { get; set; }
    public List<OrderLine> Lines { get; set; } = [];

    public bool Paid => OrderStatuses.IsPaid(Status);

    public bool Reserves(DateTime utcNow) => Status == OrderStatuses.Pending && ExpiresAt > utcNow;

    /// <summary>A new Køb nu for a priced basket. Throws when it is empty.</summary>
    public static Order For(IReadOnlyList<(StockItem Item, PricedLine Price)> lines, DateTime now)
    {
        if (lines.Count == 0) throw new InvalidOperationException("Kurven er tom.");
        var first = lines[0].Item.Line;
        return new Order
        {
            ItemId = lines[0].Item.Id,
            Line = lines.Count == 1 ? first : $"{first} + {lines.Count - 1} {(lines.Count == 2 ? "anden" : "andre")}",
            Kr = lines.Sum(l => l.Price.Kr),
            CreatedAt = now,
            ExpiresAt = now + PayWithin + TimeSpan.FromMinutes(1),
            Lines = lines.Select(l => new OrderLine
            {
                ItemId = l.Item.Id, Line = l.Item.Line, Kind = l.Item.Kind, Usage = l.Item.Usage, ListKr = l.Price.ListKr, Kr = l.Price.Kr,
                Offer = l.Price.Offer,
            }).ToList(),
        };
    }

    /// <summary>Paid: the buyer's details are kept. The copies leave the stock when Lager books Sale().</summary>
    public void Pay(PaymentState s, DateTime utcNow)
    {
        if (Paid) return;
        Status = OrderStatuses.Paid;
        PaidAt = utcNow;
        PaidKr = s.PaidKr;
        (Email, Name, Phone, Address, Delivery) = (s.Email, s.Name, s.Phone, s.Address, s.Delivery);
    }

    /// <summary>What Lager is told when it is paid: one copy per line, with what the item's note should say.</summary>
    public StockSale Sale() => new(Token, Name != "" ? Name : Email,
        Lines.Select(l => new StockSaleLine(l.ItemId, l.Kr, $"i butikken for {l.Kr} kr (ordre {Id}{(l.Offer != "" ? ", " + l.Offer : "")})")).ToList());

    /// <summary>Paid and Lager does not know yet.</summary>
    public bool NeedsBooking => Paid && !StockBooked;

    public void Expire()
    {
        if (Status == OrderStatuses.Pending) Status = OrderStatuses.Expired;
    }

    public void Pack(DateTime now)
    {
        if (Status != OrderStatuses.Paid) throw new InvalidOperationException($"Ordren kan ikke pakkes - den er {Status.ToLowerInvariant()}.");
        Status = OrderStatuses.Packed;
        PackedAt = now;
    }

    /// <summary>Sent with the post (from Betalt or Pakket - a small thing goes straight in the envelope).</summary>
    public void Send(string tracking, DateTime now)
    {
        if (Status is not (OrderStatuses.Paid or OrderStatuses.Packed)) throw new InvalidOperationException($"Ordren kan ikke sendes - den er {Status.ToLowerInvariant()}.");
        PackedAt ??= now;
        Status = OrderStatuses.Sent;
        Tracking = tracking.Trim();
        DoneAt = now;
    }

    public void PickUp(DateTime now)
    {
        if (Status is not (OrderStatuses.Paid or OrderStatuses.Packed)) throw new InvalidOperationException($"Ordren kan ikke afhentes - den er {Status.ToLowerInvariant()}.");
        PackedAt ??= now;
        Status = OrderStatuses.PickedUp;
        DoneAt = now;
    }

    /// <summary>A step back when a person pressed the wrong button: Sendt/Afhentet -> Pakket -> Betalt.</summary>
    public void Undo()
    {
        if (Status is OrderStatuses.Sent or OrderStatuses.PickedUp) { Status = OrderStatuses.Packed; DoneAt = null; Tracking = ""; }
        else if (Status == OrderStatuses.Packed) { Status = OrderStatuses.Paid; PackedAt = null; }
    }
}

/// <summary>One copy in a sale, at the price it was sold for and the offer that gave it.</summary>
public sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ItemId { get; set; }              // the item in Lager
    public string Line { get; set; } = "";
    public string Kind { get; set; } = "";
    /// <summary>Brugt or Ny when it was sold - for the bookkeeping: brugtmoms covers used goods, new goods carry ordinary moms.</summary>
    public string Usage { get; set; } = Usages.Used;
    public int ListKr { get; set; }              // the normal shop price
    public int Kr { get; set; }                  // what it was sold for
    public string Offer { get; set; } = "";      // "10 for 800 kr", "50 % på bøger" or ""
}

/// <summary>What Stripe says about a checkout.</summary>
public sealed record PaymentState(bool Paid, bool Expired, int PaidKr = 0, string Email = "", string Name = "", string Phone = "",
    string Address = "", string Delivery = "");
