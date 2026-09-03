using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using ITMartinMailTriage.Server.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinMailTriage.Server.Services;

/// <summary>
/// Reads Gmail via a local OAuth "Desktop app" client. First run opens a
/// system browser for consent; the refresh token is then cached under
/// TokenStorePath so later syncs don't prompt again.
///
/// Setup required (see README): create an OAuth client in Google Cloud
/// Console (APIs & Services -> Credentials -> Create Credentials -> OAuth
/// client ID -> Desktop app), download the JSON, and point
/// MailTriage:Gmail:CredentialsPath at it.
/// </summary>
public sealed class GmailSyncService : IMailSyncService
{
    public MailAccount Account => MailAccount.Gmail;

    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailSyncService> _logger;

    public GmailSyncService(IConfiguration configuration, ILogger<GmailSyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<FetchedEmail>> FetchRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(cancellationToken);

        var listRequest = service.Users.Messages.List("me");
        listRequest.LabelIds = "INBOX";
        listRequest.MaxResults = maxCount;

        var listResponse = await listRequest.ExecuteAsync(cancellationToken);
        if (listResponse.Messages is null) return [];

        var results = new List<FetchedEmail>(listResponse.Messages.Count);

        foreach (var msg in listResponse.Messages)
        {
            var getRequest = service.Users.Messages.Get("me", msg.Id);
            getRequest.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
            getRequest.MetadataHeaders = new Google.Apis.Util.Repeatable<string>(["From", "Subject", "Date"]);

            var full = await getRequest.ExecuteAsync(cancellationToken);
            var headers = full.Payload?.Headers ?? [];

            var from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
            var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";
            var receivedAt = full.InternalDate.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(full.InternalDate.Value)
                : DateTimeOffset.UtcNow;

            results.Add(new FetchedEmail(
                MessageId: full.Id,
                From: from,
                Subject: subject,
                Snippet: full.Snippet ?? "",
                ReceivedAtUtc: receivedAt));
        }

        return results;
    }

    private async Task<GmailService> GetServiceAsync(CancellationToken cancellationToken)
    {
        var credentialsPath = _configuration["MailTriage:Gmail:CredentialsPath"]
            ?? throw new InvalidOperationException("Missing MailTriage:Gmail:CredentialsPath config - see GmailSyncService setup notes.");
        var tokenStorePath = _configuration["MailTriage:Gmail:TokenStorePath"] ?? "data/gmail-token";

        await using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            [GmailService.Scope.GmailReadonly],
            "user",
            cancellationToken,
            new FileDataStore(tokenStorePath, true));

        _logger.LogInformation("Gmail authorized, token cached at {Path}", tokenStorePath);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ITMartin Mail Triage"
        });
    }
}
