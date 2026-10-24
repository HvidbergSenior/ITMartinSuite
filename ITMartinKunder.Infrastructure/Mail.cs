using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinKunder.Infrastructure;

// SMTP = the one.com mailbox info@itmartin.dk, the same one Hjem uses (Kunder__Smtp__Password in magic.env, set by the owner).
// Without a password nothing is sent and the page says so.
internal sealed class SmtpMailSender(IConfiguration cfg, ILogger<SmtpMailSender> log) : IMailSender
{
    private string Host => cfg["Kunder:Smtp:Host"] ?? "send.one.com";
    private int Port => int.TryParse(cfg["Kunder:Smtp:Port"], out var p) ? p : 587;
    private string User => cfg["Kunder:Smtp:User"] ?? "info@itmartin.dk";
    private string? Password => cfg["Kunder:Smtp:Password"];
    private string ReplyTo => cfg["Kunder:Smtp:ReplyTo"] ?? "ITMartin@Mensa.dk";

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Password)) { log.LogWarning("Mail not sent: Kunder__Smtp__Password is not set"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(User, "Martin Hvidberg · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
            };
            msg.ReplyToList.Add(new MailAddress(ReplyTo, "Martin Hvidberg"));
            using var smtp = new SmtpClient(Host, Port) { EnableSsl = true, Credentials = new NetworkCredential(User, Password) };
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
