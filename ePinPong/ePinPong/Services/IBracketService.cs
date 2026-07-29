using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Collections.Generic;

namespace ePinPong.Services
{
    public interface IBracketService
    {
        List<Mec> GenerirajGrupe(Turnir turnir, List<string> igracIds);
        List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze);
        List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false);
        List<TurnirPlasmanViewModel> IzracunajPlasman(Turnir turnir);
        List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi);
        void PropagirajBye(List<Mec> mecevi);
    }
}
