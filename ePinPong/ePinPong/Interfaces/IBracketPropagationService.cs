using ePinPong.Models;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    /// <summary>
    /// SRP — odgovoran isključivo za propagaciju rezultata tokom trajanja turnira:
    /// propagacija pobjednika, provjera i generisanje razigravanja, provjera BYE prolaza.
    /// </summary>
    public interface IBracketPropagationService
    {
        Task PropagirajPobjednikaAsync(Mec odigraniMec);
        Task ProvjeriIGenerirajRazigravanjaAsync(int turnirId);
        Task<(bool Success, string ErrorMessage)> GenerisiPlasmanZaRangeAsync(Turnir turnir, int plL, int plR);
    }
}
