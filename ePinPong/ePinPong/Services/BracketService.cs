using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Collections.Generic;

namespace ePinPong.Services
{
    public class BracketService : IBracketService
    {
        public const string SLOBODAN = "SLOBODAN";

        private readonly IBracketDrawService _drawService;
        private readonly IBracketProgressionService _progressionService;
        private readonly IStandingsCalculationService _standingsService;

        public BracketService(
            IBracketDrawService drawService,
            IBracketProgressionService progressionService,
            IStandingsCalculationService standingsService)
        {
            _drawService = drawService;
            _progressionService = progressionService;
            _standingsService = standingsService;
        }

        public static bool JeSlobodan(string? id) => id == SLOBODAN;

        public List<Mec> GenerirajGrupe(Turnir turnir, List<string> igracIds, bool useQualityGrouping = false)
        {
            return _drawService.GenerirajGrupe(turnir, igracIds, useQualityGrouping);
        }

        public List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
        {
            return _progressionService.GenerirajZavrsnicu(turnir, meceviGrupneFaze);
        }

        public List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false)
        {
            return _progressionService.GenerirajPlasmanFazu(turnir, L, R, gubitnici, postojeciMecevi, isUtjesni);
        }

        public void PropagirajBye(List<Mec> mecevi)
        {
            _progressionService.PropagirajBye(mecevi);
        }

        public List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi)
        {
            return _progressionService.GenerirajTurnirParova(turnir, parovi);
        }

        public List<TurnirPlasmanViewModel> IzracunajPlasman(Turnir turnir)
        {
            return _standingsService.IzracunajPlasman(turnir);
        }
    }
}
