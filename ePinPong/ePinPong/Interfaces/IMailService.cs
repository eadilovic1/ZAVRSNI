using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface IMailService
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}
