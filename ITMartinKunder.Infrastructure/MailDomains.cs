using System.Collections.Concurrent;
using DnsClient;
using Microsoft.Extensions.Logging;

namespace ITMartinKunder.Infrastructure;

// Does the domain after @ take mail (user 2026-10-29 sign-up checks)? A mail server (MX) in DNS - or, the old way, an
// address on the domain itself. Only the domain is asked about, never the person's address. Answers are kept a day,
// and a DNS that is slow or down answers "could not tell", so nobody is ever stopped by our own trouble.
internal sealed class DnsMailDomains(ILogger<DnsMailDomains> log) : IMailDomains
{
    private static readonly TimeSpan Keep = TimeSpan.FromDays(1);
    private readonly LookupClient _dns = new(new LookupClientOptions { Timeout = TimeSpan.FromSeconds(3), Retries = 1, UseCache = true });
    private readonly ConcurrentDictionary<string, (DateTime At, bool Takes)> _known = new();

    public async Task<bool?> TakesMailAsync(string domain, CancellationToken ct)
    {
        domain = domain.Trim().TrimEnd('.').ToLowerInvariant();
        if (_known.TryGetValue(domain, out var k) && DateTime.UtcNow - k.At < Keep) return k.Takes;
        try
        {
            var mx = await _dns.QueryAsync(domain, QueryType.MX, cancellationToken: ct);
            if (mx.HasError && mx.Header.ResponseCode != DnsHeaderResponseCode.NotExistentDomain) return null;   // server trouble: cannot tell
            var takes = mx.Answers.MxRecords().Any();
            if (!takes && !mx.HasError) takes = (await _dns.QueryAsync(domain, QueryType.A, cancellationToken: ct)).Answers.ARecords().Any();
            _known[domain] = (DateTime.UtcNow, takes);
            return takes;
        }
        catch (Exception ex) when (ex is DnsResponseException or OperationCanceledException or System.Net.Sockets.SocketException)
        {
            if (ct.IsCancellationRequested) throw;
            log.LogWarning("DNS could not tell if {Domain} takes mail: {Message}", domain, ex.Message);
            return null;
        }
    }
}
