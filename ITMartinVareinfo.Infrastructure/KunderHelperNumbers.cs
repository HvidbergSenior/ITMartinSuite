using ITMartinKunder.Client;
using ITMartinKunder.Contracts;

namespace ITMartinVareinfo.Infrastructure;

// Kunder answers whether a kundenummer is an admin (Kunder's Admin tick). The client keeps a yes for 10 minutes.
// Helpers were removed 2026-11-10 (only Martin takes the pictures).
internal sealed class KunderHelperNumbers(KunderClient kunder) : IHelperNumbers
{
    public async Task<string?> AdminNameAsync(string kundenummer, CancellationToken ct = default) =>
        await kunder.CheckCodeAsync(kundenummer, KunderRoles.Admin, ct) is { Ok: true } r ? (r.Name == "" ? "Admin" : r.Name) : null;
}
