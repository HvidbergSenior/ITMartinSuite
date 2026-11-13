using ITMartinElPriser.Application;
using ITMartinKunder.Client;

namespace ITMartinElPriser.Infrastructure;

// Kunder on the photoserver (Kunder__Url / Kunder__ApiKey) answers whether a kundenummer opens ElPriser's extras.
internal sealed class KunderCodes(KunderClient kunder) : ICustomerCodes
{
    public async Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, "ElPriser", ct);
        return (r.Ok, r.Error);
    }
}

// MinElpris sign-up: ok only with Martin's "MinElpris: betalt" tick on the customer in Kunder.
internal sealed class KunderPaidCodes(KunderClient kunder) : IPaidCodes
{
    public async Task<(bool Ok, string Error)> CheckPaidAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, "MinElpris", ct);
        if (r.Ok && r.Paid) return (true, "");
        if (r.Error.StartsWith("Den app er ikke med", StringComparison.Ordinal))
            return (false, "Dit nummer har ikke MinElpris endnu. Prisen aftaler I sammen – skriv eller ring til Martin (31 19 47 30), så slår han den til.");
        return (false, r.Error.Length > 0 ? r.Error : "Det nummer virker ikke i MinElpris. Spørg Martin.");
    }
}
