using ITMartinHjem.Application;
using ITMartinKunder.Client;
using ITMartinKunder.Contracts;

namespace ITMartinHjem.Infrastructure;

// The Kundekartotek is Kunder on the photoserver (2026-10-21, one list for all apps; Kunder__Url + Kunder__ApiKey).
// When Kunder does not answer, KunderClient keeps the sign-up in data/kunder-outbox and sends it later.
public sealed class KunderGateway(KunderClient kunder) : ICustomerGateway
{
    public async Task<SignUpResult> SignUpAsync(CustomerSignUp s, CancellationToken ct = default)
    {
        var (error, code) = await kunder.SignUpWithCodeAsync(new SignUpRequest(s.Email, s.Name,
            s.Company == CustomerOf.Bogshoppen ? Companies.Bogshoppen : Companies.ITMartin, s.App, s.Source, s.ConsentText, s.Interests), ct);
        return new(error, code);
    }

    public Task UnsubscribeAsync(string email, CancellationToken ct = default) => kunder.UnsubscribeAsync(email, ct);

    public async Task<IReadOnlyList<FreeAppView>> FreeAppsAsync(CancellationToken ct = default) =>
        (await kunder.FreeAppsAsync(ct)).Select(a => new FreeAppView(a.Name, a.Url, a.NeedsCode)).ToList();
}
