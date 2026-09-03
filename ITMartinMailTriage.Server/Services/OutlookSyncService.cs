using Azure.Identity;
using ITMartinMailTriage.Server.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace ITMartinMailTriage.Server.Services;

/// <summary>
/// Reads Outlook/Microsoft 365 mail via Microsoft Graph. First run opens a
/// system browser for consent (InteractiveBrowserCredential caches the
/// token itself via MSAL's token cache under the user profile, so later
/// syncs don't prompt again).
///
/// Setup required (see README): register an app in Microsoft Entra ID
/// (entra.microsoft.com -> App registrations -> New registration), add
/// "Mail.Read" delegated permission, set it up as a public client with
/// redirect URI http://localhost, and point MailTriage:Outlook:ClientId /
/// TenantId at the registered values.
/// </summary>
public sealed class OutlookSyncService : IMailSyncService
{
    public MailAccount Account => MailAccount.Outlook;

    private readonly IConfiguration _configuration;
    private readonly ILogger<OutlookSyncService> _logger;

    public OutlookSyncService(IConfiguration configuration, ILogger<OutlookSyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<FetchedEmail>> FetchRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var client = GetClient();

        var messages = await client.Me.MailFolders["Inbox"].Messages.GetAsync(config =>
        {
            config.QueryParameters.Top = maxCount;
            config.QueryParameters.Orderby = ["receivedDateTime DESC"];
            config.QueryParameters.Select = ["id", "from", "subject", "bodyPreview", "receivedDateTime"];
        }, cancellationToken);

        var results = new List<FetchedEmail>();
        foreach (var m in messages?.Value ?? [])
        {
            results.Add(new FetchedEmail(
                MessageId: m.Id ?? "",
                From: m.From?.EmailAddress?.Address ?? "",
                Subject: m.Subject ?? "",
                Snippet: m.BodyPreview ?? "",
                ReceivedAtUtc: m.ReceivedDateTime ?? DateTimeOffset.UtcNow));
        }

        return results;
    }

    private GraphServiceClient GetClient()
    {
        var clientId = _configuration["MailTriage:Outlook:ClientId"]
            ?? throw new InvalidOperationException("Missing MailTriage:Outlook:ClientId config - see OutlookSyncService setup notes.");
        var tenantId = _configuration["MailTriage:Outlook:TenantId"] ?? "common";

        var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            ClientId = clientId,
            TenantId = tenantId,
            RedirectUri = new Uri("http://localhost")
        });

        _logger.LogInformation("Outlook client created for tenant {Tenant}", tenantId);

        return new GraphServiceClient(credential, ["Mail.Read"]);
    }
}
