using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class IdentityEmailSenderAdapter : IEmailSender
    {
        private readonly IMailService _mailService;

        public IdentityEmailSenderAdapter(IMailService mailService)
        {
            _mailService = mailService;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => _mailService.SendEmailAsync(email, subject, htmlMessage);
    }
}
