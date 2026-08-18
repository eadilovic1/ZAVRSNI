using ePinPong.Helpers;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public class TurnirCompletionService : ITurnirCompletionService
    {
        private readonly IStandingsCalculationService _standingsCalculationService;

        public TurnirCompletionService(IStandingsCalculationService standingsCalculationService)
        {
            _standingsCalculationService = standingsCalculationService;
        }

        public bool EvaluateAndCloseIfFinished(Turnir turnir)
        {
            if (turnir == null || turnir.Status == StatusTurnira.Zavrsen || turnir.Mecevi == null || !turnir.Mecevi.Any())
            {
                return false;
            }

            var meceviList = turnir.Mecevi.ToList();
            var imaZavrsnicu = meceviList.Any(m => m.TipMeca == TipMeca.Zavrsnica);
            var grupniMecevi = meceviList.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var brojGrupa = grupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();
            var isMasters = LigaTurnirHelper.IsMastersTurnir(turnir);
            var isGroupOnly = grupniMecevi.Any() && !imaZavrsnicu && (brojGrupa == 1 || isMasters);

            if (!meceviList.All(m => m.Odigran && m.TipMeca != TipMeca.TurnirParova))
            {
                return false;
            }

            if (imaZavrsnicu || isGroupOnly || isMasters)
            {
                turnir.Status = StatusTurnira.Zavrsen;

                if (imaZavrsnicu)
                {
                    var finalMec = meceviList.FirstOrDefault(m => m.TipMeca == TipMeca.Zavrsnica && string.IsNullOrEmpty(m.MecKodovi?.WinnerNextMatchCode));
                    if (finalMec != null && finalMec.Odigran)
                    {
                        // Plasman se ne čuva denormalizovano — izračunava se iz Mecevi putem IzracunajPlasman
                    }
                }
                else if (grupniMecevi.Any())
                {
                    var tables = _standingsCalculationService.IzracunajTabeleGrupa(turnir);
                    // Plasman se ne čuva denormalizovano — izračunava se iz Mecevi putem IzracunajPlasman
                }

                return true;
            }

            return false;
        }
    }
}
