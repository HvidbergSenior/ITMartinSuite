using ITMartinKunder.Client;
using ITMartinKunder.Contracts;
using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinPoll.Infrastructure;

// Stem's customers go to Kunder (2026-10-21, one Kundekartotek for all apps; Kunder__Url / Kunder__ApiKey).
// What Kunder does not take right now waits in data/kunder-outbox.
internal sealed class KunderCustomerStore(KunderClient kunder) : ICustomerStore
{
    public async Task UpsertAsync(CustomerOptIn o, CancellationToken ct = default)
    {
        var error = await kunder.SignUpAsync(new SignUpRequest(o.Email, o.Name, CustomerRules.CompanyOf(o.Source), "Stem", o.Source,
            o.ConsentText, o.Interests, o.AtUtc), ct);
        if (error is not null) throw new InvalidOperationException(error);   // the e-mail was checked before - should not happen
    }

    public Task UnsubscribeAsync(string email, CancellationToken ct = default) => kunder.UnsubscribeAsync(email, ct);

    public async Task<(bool Found, string Email)> UnsubscribeByLinkAsync(string token, CancellationToken ct = default)
    {
        var r = await kunder.UnsubscribeByLinkAsync(token, ct);
        return (r.Found, r.Email);
    }

    public Task MoveAsync(Customer c, CancellationToken ct = default) =>
        kunder.ImportAsync(new ImportRequest(c.Email, c.Name, c.Phone, CustomerRules.CompanyOf(c.Source), "Stem", c.Source, c.ConsentText,
            DateTime.SpecifyKind(c.ConsentAt, DateTimeKind.Utc), c.Interests, c.Note, c.UnsubscribeToken,
            c.UnsubscribedAt is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null, DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc)), ct);
}

// Kunder answers whether a kundenummer opens Stem's extras (making your own poll).
internal sealed class KunderCodes(KunderClient kunder) : ICustomerCodes
{
    public async Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, "Stem", ct);
        return (r.Ok, r.Error);
    }
}

// Once: Stem's old list (poll.db Customers) into Kunder. The mark file stops it from running again, so a customer
// Martin deletes in Kunder (GDPR) is never brought back by a Stem restart.
internal sealed class MoveOldCustomersJob(IServiceProvider services, string markFile) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (File.Exists(markFile)) return;
        await Task.Delay(TimeSpan.FromSeconds(20), ct);   // let the app start first
        using var scope = services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<MoveOldCustomersJob>>();
        var moved = await scope.ServiceProvider.GetRequiredService<CustomerList>()
            .MoveOldListAsync(scope.ServiceProvider.GetRequiredService<IAggregateStore<Customer>>(), ct);
        await File.WriteAllTextAsync(markFile, $"{moved} kunder flyttet til Kunder {DateTime.UtcNow:O}" + Environment.NewLine, ct);
        log.LogInformation("Moved {Count} customers from poll.db to Kunder (sent now or waiting in the outbox)", moved);
    }
}
