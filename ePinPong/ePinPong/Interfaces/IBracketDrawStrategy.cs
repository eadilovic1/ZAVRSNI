using ePinPong.Models;
using System.Collections.Generic;

namespace ePinPong.Interfaces
{
    /// <summary>
    /// Strategy pattern: enkapsulira algoritam generisanja završnog dijela
    /// bracketa za jedan konkretan <see cref="SistemTurnira"/>.
    /// Svaka implementacija zna samo za "svoj" sistem, čime se izbjegava
    /// grananje (if/switch na SistemTurnira) unutar jedne velike metode.
    /// </summary>
    public interface IBracketDrawStrategy
    {
        /// <summary>Sistem takmičenja za koji je ova strategija zadužena.</summary>
        SistemTurnira Sistem { get; }

        /// <summary>Generiše mečeve završnice na osnovu rezultata grupne faze.</summary>
        List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze);
    }
}
