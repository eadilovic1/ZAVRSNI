using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface INotificationService
    {
        Task ObavijestiKorisnikaAsync(string userId, string naslov, string poruka, bool posaljiEmail = true);
    }
}
