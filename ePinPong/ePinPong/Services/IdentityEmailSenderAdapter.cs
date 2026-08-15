using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class IdentityEmailSenderAdapter : IEmailSender
    {
        private readonly IEmailQueueService _emailQueue;

        public IdentityEmailSenderAdapter(IEmailQueueService emailQueue)
        {
            _emailQueue = emailQueue;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _emailQueue.Enqueue(email, subject, htmlMessage);
            return Task.CompletedTask;
        }
    }
}
