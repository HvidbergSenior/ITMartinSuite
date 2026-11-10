using System.Net;
using System.Net.Mail;
using System.Text;
using ITMartinSnak.Application;
using Microsoft.Extensions.Logging;

namespace ITMartinSnak.Infrastructure;

/// <summary>The one.com mailbox info@itmartin.dk (same as Kunder and Hjem). Answers go to Martin (ReplyTo).</summary>
public sealed record SmtpSettings(string Host, int Port, string User, string? Password, string ReplyTo);

internal sealed class SmtpRoomMailer(SmtpSettings s, ILogger<SmtpRoomMailer> log) : IRoomMailer
{
    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.Password)) { log.LogWarning("Mail not sent: no SMTP password (Snak__Smtp__Password or Kunder__Smtp__Password)"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(s.User, "Martin Hvidberg · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
            };
            msg.ReplyToList.Add(new MailAddress(s.ReplyTo, "Martin Hvidberg"));
            using var smtp = new SmtpClient(s.Host, s.Port) { EnableSsl = true, Credentials = new NetworkCredential(s.User, s.Password) };
            await smtp.SendMailAsync(msg, ct);
            log.LogInformation("Mail sent: {Subject}", subject);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail failed");
            return false;
        }
    }
}
