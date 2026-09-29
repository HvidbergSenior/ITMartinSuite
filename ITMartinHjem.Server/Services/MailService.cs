using System.Net;
using System.Net.Mail;

namespace ITMartinHjem.Server.Services;

// Sends the "Martin har svaret dig" mail to a chat visitor who left an email address.
// SMTP = the one.com mailbox info@itmartin.dk (Hjem__Smtp__Password in magic.env, set by the owner).
// Without a password nothing is sent - the chat and the browser notification still work.
public sealed class MailService(IConfiguration cfg, ILogger<MailService> log)
{
    private string Host => cfg["Hjem:Smtp:Host"] ?? "send.one.com";
    private int Port => cfg.GetValue("Hjem:Smtp:Port", 587);
    private string User => cfg["Hjem:Smtp:User"] ?? "info@itmartin.dk";
    private string? Password => cfg["Hjem:Smtp:Password"];
    private string ReplyTo => cfg["Hjem:Smtp:ReplyTo"] ?? "ITMartin@Mensa.dk";

    public bool Configured => !string.IsNullOrWhiteSpace(Password);

    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        if (!Configured) { log.LogWarning("Mail not sent to a visitor: Hjem__Smtp__Password is not set"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(User, "Martin Hvidberg · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body, BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8,
            };
            msg.ReplyToList.Add(new MailAddress(ReplyTo, "Martin Hvidberg"));
            using var smtp = new SmtpClient(Host, Port) { EnableSsl = true, Credentials = new NetworkCredential(User, Password) };
            await smtp.SendMailAsync(msg);
            log.LogInformation("Reply mail sent to a visitor");
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Reply mail to a visitor failed");
            return false;
        }
    }

    public static bool LooksLikeEmail(string s) =>
        s.Length is > 5 and < 200 && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
