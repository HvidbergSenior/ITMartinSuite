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
