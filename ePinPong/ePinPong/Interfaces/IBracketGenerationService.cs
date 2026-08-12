using ePinPong.Models;
using System.Collections.Generic;

namespace ePinPong.Services
{
    /// <summary>
    /// SRP — odgovoran isključivo za kreiranje mečeva i rundi:
    /// završnica, utješni bracket, razigravanje za plasman, turnir parova.
    /// </summary>
    public interface IBracketGenerationService
    {
        List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze);
        List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi);
        List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false);

        /// <summary>
        /// Automatski odigrava mečeve u kojima barem jedan igrač je SLOBODAN (BYE),
        /// i propaguje pobjednika u naredne mečeve. Radi na in-memory listi — bez DB pristupa.
        /// </summary>
        void PropagirajBye(List<Mec> mecevi);
    }
}
