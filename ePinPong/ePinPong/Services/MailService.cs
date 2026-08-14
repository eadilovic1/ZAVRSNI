using ePinPong.Models.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using System;
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
            _logger.LogInformation("[MailService] Slanje emaila na {Email} | Predmet: {Subject}", email, subject);

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
                // Namjerno ne bacamo exception dalje — slanje maila je "best effort" (fail-safe)
                // i ne smije srušiti primarnu operaciju u aplikaciji.
                _logger.LogError(ex, "[MailService] Greška prilikom slanja emaila na {Email} preko SMTP servera ({Host}:{Port}).", email, _smtpOptions.Host, _smtpOptions.Port);
            }
            finally
            {
                try
                {
                    await client.DisconnectAsync(true);
                }
                catch
                {
                    // Ignorišemo greške prilikom odspajanja ako konekcija nije u potpunosti uspostavljena
                }
            }
        }
    }
}
