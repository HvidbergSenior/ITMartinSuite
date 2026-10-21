using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ITMartinSalg.Infrastructure.External;

// Stripe Checkout over its REST API (no SDK): a hosted payment page per order - card, MobilePay and whatever else the
// Stripe account has switched on. Salg__StripeSecretKey in magic.env (sk_live_... or sk_test_...), never in the repo.
// No webhook: the buyer's return and OrderJob (every minute) ask Stripe whether it was paid.
public sealed class StripeCheckout(HttpClient http, IConfiguration cfg) : IPaymentGateway
{
    private string Key => cfg["Salg:StripeSecretKey"] ?? "";
    public bool Enabled => Key.StartsWith("sk_") || Key.StartsWith("rk_");

    private HttpRequestMessage Req(HttpMethod m, string path, IEnumerable<KeyValuePair<string, string>>? form = null)
    {
        var req = new HttpRequestMessage(m, "https://api.stripe.com/v1" + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        if (form is not null) req.Content = new FormUrlEncodedContent(form);
        return req;
    }

    public async Task<(string CheckoutId, string Url)> StartAsync(Order order, string description, string successUrl, string cancelUrl,
        int shippingKr, string pickup, CancellationToken ct)
    {
        using var doc = await SendAsync(Req(HttpMethod.Post, "/checkout/sessions", Form(order, description, successUrl, cancelUrl, shippingKr, pickup)), ct);
        return (doc.RootElement.GetProperty("id").GetString()!, doc.RootElement.GetProperty("url").GetString()!);
    }

    /// <summary>The checkout session as Stripe's form fields. Public for the tests.</summary>
    public static List<KeyValuePair<string, string>> Form(Order order, string description, string successUrl, string cancelUrl, int shippingKr, string pickup)
    {
        var f = new List<KeyValuePair<string, string>>();
        void Add(string k, string v) => f.Add(new(k, v));
        static string N(long x) => x.ToString(CultureInfo.InvariantCulture);
        Add("mode", "payment");
        Add("locale", "da");
        Add("success_url", successUrl);
        Add("cancel_url", cancelUrl);
        Add("client_reference_id", N(order.Id));
        Add("metadata[order]", N(order.Id));
        Add("expires_at", N(new DateTimeOffset(DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc) + Order.PayWithin).ToUnixTimeSeconds()));
        // One payment line per thing, at today's price, with the offer that gave it - the receipt shows the basket.
        var lines = order.Lines.Count > 0
            ? order.Lines.Select(l => (Name: l.Line, Kr: l.Kr, Text: l.Offer != "" ? $"{l.Kind} · {l.Offer}" : l.Kind)).ToList()
            : [(Name: order.Line, Kr: order.Kr, Text: description)];
        for (var i = 0; i < lines.Count; i++)
        {
            Add($"line_items[{i}][quantity]", "1");
            Add($"line_items[{i}][price_data][currency]", "dkk");
            Add($"line_items[{i}][price_data][unit_amount]", N(lines[i].Kr * 100L));
            Add($"line_items[{i}][price_data][product_data][name]", Cut(lines[i].Name, 250));
            if (lines[i].Text != "") Add($"line_items[{i}][price_data][product_data][description]", Cut(lines[i].Text, 250));
        }
        Add("phone_number_collection[enabled]", "true");
        var n = 0;
        if (pickup != "")
        {
            Add($"shipping_options[{n}][shipping_rate_data][type]", "fixed_amount");
            Add($"shipping_options[{n}][shipping_rate_data][display_name]", Cut("Afhentning – " + pickup, 100));
            Add($"shipping_options[{n}][shipping_rate_data][fixed_amount][amount]", "0");
            Add($"shipping_options[{n}][shipping_rate_data][fixed_amount][currency]", "dkk");
            n++;
        }
        if (shippingKr > 0)
        {
            Add($"shipping_options[{n}][shipping_rate_data][type]", "fixed_amount");
            Add($"shipping_options[{n}][shipping_rate_data][display_name]", "Send med posten");
            Add($"shipping_options[{n}][shipping_rate_data][fixed_amount][amount]", N(shippingKr * 100L));
            Add($"shipping_options[{n}][shipping_rate_data][fixed_amount][currency]", "dkk");
            Add("shipping_address_collection[allowed_countries][0]", "DK");
        }
        return f;
    }

    public async Task<PaymentState> StateAsync(string checkoutId, CancellationToken ct)
    {
        using var doc = await SendAsync(Req(HttpMethod.Get, "/checkout/sessions/" + Uri.EscapeDataString(checkoutId)), ct);
        return Parse(doc.RootElement);
    }

    /// <summary>Stripe's checkout session -> paid/expired + the buyer. Public for the tests.</summary>
    public static PaymentState Parse(JsonElement s)
    {
        var paid = Str(s, "payment_status") is "paid" or "no_payment_required";
        var expired = Str(s, "status") == "expired";
        var cust = Obj(s, "customer_details");
        // Newer API versions put the shipping address under collected_information.
        var ship = Obj(s, "shipping_details") is { ValueKind: JsonValueKind.Object } sd ? sd : Obj(Obj(s, "collected_information"), "shipping_details");
        var shippingKr = Obj(s, "shipping_cost") is { ValueKind: JsonValueKind.Object } sc && sc.TryGetProperty("amount_total", out var at)
            && at.ValueKind == JsonValueKind.Number ? at.GetInt64() : 0;
        var total = s.TryGetProperty("amount_total", out var t) && t.ValueKind == JsonValueKind.Number ? (int)(t.GetInt64() / 100) : 0;
        var address = Address(Obj(ship, "address"));
        if (address == "") address = Address(Obj(cust, "address"));
        return new PaymentState(paid, expired, total, Str(cust, "email"),
            Str(ship, "name") != "" ? Str(ship, "name") : Str(cust, "name"), Str(cust, "phone"), address,
            shippingKr > 0 ? "Send med posten" : "Afhentning");
    }

    private static JsonElement Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

    private static string Address(JsonElement a) => a.ValueKind != JsonValueKind.Object ? "" : string.Join(", ", new[]
    {
        Str(a, "line1"), Str(a, "line2"), (Str(a, "postal_code") + " " + Str(a, "city")).Trim(),
    }.Where(x => x != ""));

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];

    private async Task<JsonDocument> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using (req)
        using (var res = await http.SendAsync(req, ct))
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var msg = "";
                try { msg = JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString() ?? ""; } catch { }
                throw new SalgException("Betalingen kunne ikke startes" + (msg != "" ? $" (Stripe: {msg})" : "") + ". Prøv igen om lidt.");
            }
            return JsonDocument.Parse(body);
        }
    }
}
