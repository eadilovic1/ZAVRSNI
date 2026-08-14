using ePinPong.Models.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class MailService : IMailService
    {
        private readonly SmtpOptions _smtpOptions;
        private readonly ILogger<MailService> _logger;

        public MailService(IOptions<SmtpOptions> smtpOptions, ILogger<MailService> logger)
        {
            _smtpOptions = smtpOptions.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // 1. Zadržavanje logiranja u terminalu i Debug konzoli (fake/console prikaz)
            Console.WriteLine($"[MailService] Slanje maila na: {email}");
            Console.WriteLine($"[MailService] Predmet: {subject}");
            Debug.WriteLine($"[MailService] Email poslan na: {email}");
            Debug.WriteLine($"[MailService] Predmet: {subject}");
            Debug.WriteLine($"[MailService] Sadrzaj: {htmlMessage}");
            _logger.LogInformation("[MailService] Slanje emaila na {Email} | Predmet: {Subject}", email, subject);

            // 2. Pravo slanje maila putem MailKit SMTP klijenta
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("ePinPong", _smtpOptions.Username));
            emailMessage.To.Add(MailboxAddress.Parse(email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(TextFormat.Html) { Text = htmlMessage };

            using var client = new SmtpClient();
            try
            {
                var socketOptions = _smtpOptions.EnableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, socketOptions);

                if (!string.IsNullOrEmpty(_smtpOptions.Username) && !string.IsNullOrEmpty(_smtpOptions.Password))
                {
                    await client.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password);
                }

                await client.SendAsync(emailMessage);
                _logger.LogInformation("[MailService] Email uspješno poslan na {Email} preko SMTP servera ({Host}:{Port}).", email, _smtpOptions.Host, _smtpOptions.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MailService] Greška prilikom slanja emaila na {Email} preko SMTP servera ({Host}:{Port}).", email, _smtpOptions.Host, _smtpOptions.Port);
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}
