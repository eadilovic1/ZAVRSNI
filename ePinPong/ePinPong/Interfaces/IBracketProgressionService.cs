using ePinPong.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface IBracketProgressionService
    {
        List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze);
        List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false);
        void PropagirajBye(List<Mec> mecevi);
        List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi);
        Task PropagirajPobjednikaAsync(Mec odigraniMec);
        Task ProvjeriIGenerirajRazigravanjaAsync(int turnirId);
        Task<(bool Success, string ErrorMessage)> GenerisiPlasmanZaRangeAsync(Turnir turnir, int plL, int plR);
    }
}
