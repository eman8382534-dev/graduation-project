using GrawiaaApp.API.Interfaces;
using System.Net;
using System.Net.Mail;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    public EmailSender(IConfiguration config) => _config = config;

    public Task SendEmailAsync(string email, string subject, string message)
    {
        var mail = _config.GetSection("EmailSettings");

        // FIX CS8604: use null-coalescing to avoid possible null dereference on int.Parse
        var host     = mail["Host"]     ?? throw new InvalidOperationException("EmailSettings:Host is not configured.");
        var portStr  = mail["Port"]     ?? throw new InvalidOperationException("EmailSettings:Port is not configured.");
        var username = mail["Username"] ?? throw new InvalidOperationException("EmailSettings:Username is not configured.");
        var password = mail["Password"] ?? throw new InvalidOperationException("EmailSettings:Password is not configured.");
        var sender   = mail["SenderEmail"] ?? throw new InvalidOperationException("EmailSettings:SenderEmail is not configured.");

        var client = new SmtpClient(host, int.Parse(portStr))
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

        var mailMessage = new MailMessage(sender, email, subject, message)
        {
            IsBodyHtml = true
        };

        return client.SendMailAsync(mailMessage);
    }
}
