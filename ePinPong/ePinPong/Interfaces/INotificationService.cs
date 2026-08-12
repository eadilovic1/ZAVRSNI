using ePinPong.Models;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface INotificationService
    {
        Task ObavijestiKorisnikaAsync(string userId, string naslov, string poruka, string? emailPoruka = null, bool posaljiEmail = true);
        Task ObavijestiKorisnikaAsync(ApplicationUser korisnik, string naslov, string poruka, string? emailPoruka = null, bool posaljiEmail = true);
    }
}
