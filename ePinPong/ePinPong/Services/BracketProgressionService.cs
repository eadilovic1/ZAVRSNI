using ePinPong.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    /// <summary>
    /// Fasada koja implementira <see cref="IBracketProgressionService"/> delegiranjem na
    /// <see cref="IBracketGenerationService"/> i <see cref="IBracketPropagationService"/>.
    /// <para>
    /// Svrha: zadržati kompatibilnost s postojećim kontrolerima i DI registracijama
    /// bez ijedne izmjene u kontrolerima — oni i dalje injektuju <c>IBracketProgressionService</c>.
    /// </para>
    /// </summary>
    public class BracketProgressionService : IBracketProgressionService
    {
        private readonly IBracketGenerationService  _generation;
        private readonly IBracketPropagationService _propagation;

        /// <summary>
        /// Backward-compatible statički forwarder — zadržan kako bi postojeći
        /// pozivi <c>BracketProgressionService.JeSlobodan(...)</c> nastavili da rade.
        /// </summary>
        public static bool JeSlobodan(string? id) => BracketGenerationService.JeSlobodan(id);

        public BracketProgressionService(
            IBracketGenerationService  generation,
            IBracketPropagationService propagation)
        {
            _generation  = generation;
            _propagation = propagation;
        }

        public List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
            => _generation.GenerirajZavrsnicu(turnir, meceviGrupneFaze);

        public List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R,
            List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false)
            => _generation.GenerirajPlasmanFazu(turnir, L, R, gubitnici, postojeciMecevi, isUtjesni);

        public void PropagirajBye(List<Mec> mecevi)
            => _generation.PropagirajBye(mecevi);

        public List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi)
            => _generation.GenerirajTurnirParova(turnir, parovi);

        public Task PropagirajPobjednikaAsync(Mec odigraniMec)
            => _propagation.PropagirajPobjednikaAsync(odigraniMec);

        public Task ProvjeriIGenerirajRazigravanjaAsync(int turnirId)
            => _propagation.ProvjeriIGenerirajRazigravanjaAsync(turnirId);

        public Task<(bool Success, string ErrorMessage)> GenerisiPlasmanZaRangeAsync(Turnir turnir, int plL, int plR)
            => _propagation.GenerisiPlasmanZaRangeAsync(turnir, plL, plR);
    }
}
