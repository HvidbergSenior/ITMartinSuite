using ITMartinKunder.Client;
using ITMartinKunder.Contracts;

namespace ITMartinVareinfo.Infrastructure;

// Kunder answers whether a kundenummer is a helper (Martin's Hjælper tick). The client keeps a yes for 10 minutes.
internal sealed class KunderHelperNumbers(KunderClient kunder) : IHelperNumbers
{
    public async Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default) =>
        await kunder.CheckCodeAsync(kundenummer, KunderRoles.Helper, ct) is { Ok: true } r ? (r.Name == "" ? "Hjælper" : r.Name) : null;
}
