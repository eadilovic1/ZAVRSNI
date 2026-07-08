using ePinPong.Interfaces;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class MailService : IMailService
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Mock slanje emaila - upisujemo u Debug konzolu
            Debug.WriteLine($"[Mock MailService] Email poslan na: {email}");
            Debug.WriteLine($"[Mock MailService] Predmet: {subject}");
            Debug.WriteLine($"[Mock MailService] Sadrzaj: {htmlMessage}");
            
            return Task.CompletedTask;
        }
    }
}
