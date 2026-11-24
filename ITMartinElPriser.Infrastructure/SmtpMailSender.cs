using System.Net;
using System.Net.Mail;
using ITMartinElPriser.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Infrastructure;

// MinElpris mail (password reset, 2026-11-24): FROM ITMartin@mensa.dk through ImprovMX's SMTP - the same route Gmail's
// "send as" uses (user's choice 2026-11-24: never show the private Gmail). mensa.dk has DMARC p=reject, so the mail must
// leave through ImprovMX; through smtp.gmail.com it is refused ("550 5.7.26 Unauthenticated email from mensa.dk").
// magic.env: MitEl__Smtp__Password = the ImprovMX SMTP password (Host/User/Port default to smtp.improvmx.com /
// ITMartin@mensa.dk / 587). Without the password nothing is sent and the page tells the visitor to write or call Martin.
public sealed class SmtpMailSender(IConfiguration cfg, ILogger<SmtpMailSender> log) : IMailSender
{
    private string Host => cfg["MitEl:Smtp:Host"] is { Length: > 0 } h ? h : "smtp.improvmx.com";
    private int Port => int.TryParse(cfg["MitEl:Smtp:Port"], out var p) ? p : 587;
    private string User => cfg["MitEl:Smtp:User"] is { Length: > 0 } u ? u : "ITMartin@mensa.dk";
    private string? Password => cfg["MitEl:Smtp:Password"];
    private string From => cfg["MitEl:Smtp:From"] is { Length: > 0 } f ? f : User;

    public bool Configured => !string.IsNullOrWhiteSpace(Password);

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!Configured) { log.LogWarning("Mail not sent: MitEl__Smtp__Password (ImprovMX) is not set"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(From, "MinElpris · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body,
                BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8,
            };
            msg.ReplyToList.Add(new MailAddress("ITMartin@Mensa.dk", "Martin Hvidberg"));
            using var smtp = new SmtpClient(Host, Port) { EnableSsl = true, Credentials = new NetworkCredential(User, Password) };
            await smtp.SendMailAsync(msg, ct);
            log.LogInformation("Mail sent: {Subject}", subject);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail failed: {Subject}", subject);
            return false;
        }
    }
}
