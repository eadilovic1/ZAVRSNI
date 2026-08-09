using ePinPong.Models;

namespace ePinPong.Services
{
    public interface ITurnirCompletionService
    {
        /// <summary>
        /// Provjerava da li su ispunjeni uslovi za zatvaranje turnira (svi mečevi odigrani, izračun pobjednika/drugoplasiranog).
        /// Vraća true ako je status turnira promijenjen na Završen ili su ažurirani pobjednici.
        /// </summary>
        bool EvaluateAndCloseIfFinished(Turnir turnir);
    }
}
